// =============================================================================
// VoidOrderCommand  (CanteenManagementSystem.Application.Orders.Commands)
// -----------------------------------------------------------------------------
// Operator-driven cancel/refund for a single order.
//
// SEMANTICS by source status
//   Placed / Confirmed / Preparing / Ready / Pending
//        → wallet.ReleaseAsync (free the block) + inventory.ReleaseAsync (restore stock)
//        → order.Status = Cancelled
//
//   Delivered
//        → wallet.RefundAsync (reverse the deduction) + inventory.ReleaseAsync
//          (only same-day; otherwise stock is whatever it is)
//        → order.Status = Refunded
//
//   Cancelled / Refunded
//        → no-op (already terminal)
//
// AUDIT
//   One AuditEntry "Order.Voided" or "Order.Refunded" with reason + amount.
//
// ADR 0004: data access via IUnitOfWork — no IAppDbContext.
// =============================================================================

using Platform.Application.Abstractions.Audit;
using Platform.Application.Abstractions.Inventory;
using Platform.Application.Abstractions.Notifications;
using Platform.Application.Abstractions.Time;
using Platform.Application.Abstractions.Wallets;
using Platform.Application.Dispatch;
using Platform.Application.Persistence;
using CanteenManagementSystem.Application.Orders.Dtos;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Menu;
using CanteenManagementSystem.Domain.Orders;
using CanteenManagementSystem.Domain.Users;
using Platform.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanteenManagementSystem.Application.Orders.Commands;

/// <summary>
/// Cancel (pre-delivery) or refund (post-delivery) an order. The <paramref name="Amount"/>
/// parameter enables partial refunds for already-delivered orders — pass null to
/// refund the full order amount. Partial amounts on a not-yet-delivered order are
/// ignored: pre-delivery voids always release the full wallet block + restock fully.
/// </summary>
public sealed record VoidOrderCommand(int OrderId, string? Reason, decimal? Amount = null)
    : ICommand<OrderStatusUpdateResultDto>;

internal sealed class VoidOrderCommandHandler : IRequestHandler<VoidOrderCommand, OrderStatusUpdateResultDto>
{
    private readonly IUnitOfWork _uow;
    private readonly IReadOnlyRepository<DailyMenu> _menus;
    private readonly IReadOnlyRepository<Student> _students;
    private readonly IReadOnlyRepository<Employee> _employees;
    private readonly IWalletService _wallet;
    private readonly IInventoryService _inventory;
    private readonly IAuditTrail _audit;
    private readonly IClock _clock;
    private readonly INotificationService _notifications;
    private readonly ILogger<VoidOrderCommandHandler> _logger;

    public VoidOrderCommandHandler(
        IUnitOfWork uow,
        IReadOnlyRepository<DailyMenu> menus,
        IReadOnlyRepository<Student> students,
        IReadOnlyRepository<Employee> employees,
        IWalletService wallet, IInventoryService inventory,
        IAuditTrail audit, IClock clock, INotificationService notifications,
        ILogger<VoidOrderCommandHandler> logger)
    {
        _uow = uow; _menus = menus; _students = students; _employees = employees;
        _wallet = wallet; _inventory = inventory;
        _audit = audit; _clock = clock; _notifications = notifications; _logger = logger;
    }

    public async Task<OrderStatusUpdateResultDto> HandleAsync(VoidOrderCommand command, CancellationToken cancellationToken)
    {
        var orders = _uow.Repository<Order>();

        var order = await orders.Query()
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.OrderID == command.OrderId, cancellationToken);

        if (order is null) return new OrderStatusUpdateResultDto { Success = false, Message = "Order not found." };

        if (order.Status is CanteenOrderStatus.Cancelled or CanteenOrderStatus.Refunded)
            return new OrderStatusUpdateResultDto { Success = false, Message = "Order is already in a terminal state." };

        var wasDelivered = order.Status == CanteenOrderStatus.Delivered;
        var reason = string.IsNullOrWhiteSpace(command.Reason)
            ? (wasDelivered ? "Refund by operator" : "Voided by operator")
            : command.Reason!;

        // Resolve refund amount. Partial refunds only apply to delivered orders.
        var isPartial = wasDelivered
            && command.Amount is decimal req
            && req > 0
            && req < order.TotalAmount;
        var refundAmount = isPartial ? command.Amount!.Value : order.TotalAmount;
        if (wasDelivered && command.Amount is decimal a && a <= 0)
            return new OrderStatusUpdateResultDto { Success = false, Message = "Refund amount must be positive." };
        if (wasDelivered && command.Amount is decimal a2 && a2 > order.TotalAmount)
            return new OrderStatusUpdateResultDto { Success = false, Message = "Refund amount exceeds order total." };

        // Stock restoration — best-effort even if reservation row no longer matches
        // today's menu. SKIPPED on partial post-delivery refunds: the food was
        // consumed; restocking would inflate inventory.
        if (!isPartial)
        {
            foreach (var item in order.OrderItems)
            {
                var dailyMenuId = await _menus.NoTrackingQuery()
                    .Where(dm => dm.FoodItemID == item.FoodItemID && dm.MenuDate.Date == order.OrderDate.Date)
                    .Select(dm => dm.DailyMenuID)
                    .FirstOrDefaultAsync(cancellationToken);
                if (dailyMenuId > 0)
                {
                    await _inventory.ReleaseAsync(dailyMenuId, item.Quantity, cancellationToken);
                }
            }
        }

        if (wasDelivered)
        {
            var partialNote = isPartial ? $" (partial: {refundAmount:N2} of {order.TotalAmount:N2})" : string.Empty;
            var r = await _wallet.RefundAsync(order.UserId, order.UserType.ToString(), refundAmount, order.OrderID, reason + partialNote, cancellationToken);
            if (r.IsFailure) return new OrderStatusUpdateResultDto { Success = false, Message = r.Error.Message };
            // Partial refunds leave the order Delivered (it WAS delivered) — only a
            // full refund flips status to Refunded.
            if (!isPartial) order.Status = CanteenOrderStatus.Refunded;
        }
        else
        {
            var r = await _wallet.ReleaseAsync(order.UserId, order.UserType.ToString(), order.TotalAmount, order.OrderID, reason, cancellationToken);
            if (r.IsFailure) return new OrderStatusUpdateResultDto { Success = false, Message = r.Error.Message };
            order.Status = CanteenOrderStatus.Cancelled;
        }

        await _audit.RecordAsync(
            action: wasDelivered ? (isPartial ? "Order.PartialRefunded" : "Order.Refunded") : "Order.Voided",
            entityType: nameof(Order),
            entityId: order.OrderID.ToString(),
            payload: new { order.OrderNumber, order.TotalAmount, refundAmount, isPartial, reason },
            cancellationToken: cancellationToken);

        await TrySendStatusSmsAsync(order, wasDelivered, refundAmount, isPartial, cancellationToken);

        return new OrderStatusUpdateResultDto
        {
            Success = true,
            Message = wasDelivered
                ? (isPartial
                    ? $"Partial refund of {refundAmount:N2} credited; order remains delivered."
                    : "Order refunded and wallet credited.")
                : "Order voided and wallet released."
        };
    }

    private async Task TrySendStatusSmsAsync(Order order, bool wasRefund, decimal refundAmount, bool isPartial, CancellationToken ct)
    {
        try
        {
            string? phone = order.UserType == CanteenUserType.Student
                ? await _students.NoTrackingQuery().Where(s => s.ExternalId == order.UserId).Select(s => s.ContactNo).FirstOrDefaultAsync(ct)
                : await _employees.NoTrackingQuery().Where(e => e.ExternalId == order.UserId).Select(e => e.ContactNo).FirstOrDefaultAsync(ct);
            if (string.IsNullOrWhiteSpace(phone)) return;
            var body = wasRefund
                ? (isPartial
                    ? $"Order #{order.OrderNumber} partial refund — ৳{refundAmount:N0} credited to your wallet."
                    : $"Order #{order.OrderNumber} refunded — ৳{refundAmount:N0} returned to your wallet.")
                : $"Order #{order.OrderNumber} cancelled — ৳{order.TotalAmount:N0} unblocked from your wallet.";
            await _notifications.SendRawAsync(NotificationChannel.Sms, phone!, subject: null, body: body, cancellationToken: ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Void-SMS failed for order {Order}", order.OrderID); }
    }
}
