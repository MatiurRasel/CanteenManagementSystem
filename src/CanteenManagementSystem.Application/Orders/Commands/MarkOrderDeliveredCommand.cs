// =============================================================================
// MarkOrderDeliveredCommand
// -----------------------------------------------------------------------------
// ADR 0004: data access via IUnitOfWork — no IAppDbContext.
// =============================================================================

using Platform.Application.Abstractions.Audit;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Abstractions.Inventory;
using Platform.Application.Abstractions.Loyalty;
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

public sealed record MarkOrderDeliveredCommand(int OrderId) : ICommand<OrderStatusUpdateResultDto>;

internal sealed class MarkOrderDeliveredCommandHandler : IRequestHandler<MarkOrderDeliveredCommand, OrderStatusUpdateResultDto>
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
    private readonly ILoyaltyService _loyalty;
    private readonly ITenantSettings _settings;
    private readonly ILogger<MarkOrderDeliveredCommandHandler> _logger;

    public MarkOrderDeliveredCommandHandler(
        IUnitOfWork uow,
        IReadOnlyRepository<DailyMenu> menus,
        IReadOnlyRepository<Student> students,
        IReadOnlyRepository<Employee> employees,
        IWalletService wallet,
        IInventoryService inventory,
        IAuditTrail audit,
        IClock clock,
        INotificationService notifications,
        ILoyaltyService loyalty,
        ITenantSettings settings,
        ILogger<MarkOrderDeliveredCommandHandler> logger)
    {
        _uow = uow;
        _menus = menus;
        _students = students;
        _employees = employees;
        _wallet = wallet;
        _inventory = inventory;
        _audit = audit;
        _clock = clock;
        _notifications = notifications;
        _loyalty = loyalty;
        _settings = settings;
        _logger = logger;
    }

    public async Task<OrderStatusUpdateResultDto> HandleAsync(MarkOrderDeliveredCommand command, CancellationToken cancellationToken)
    {
        var orders = _uow.Repository<Order>();

        var order = await orders.Query()
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.OrderID == command.OrderId, cancellationToken);

        if (order is null) return new OrderStatusUpdateResultDto { Success = false, Message = "Order not found." };

        OrderLifecycle.EnsureCanTransition(order.Status, CanteenOrderStatus.Delivered);

        foreach (var item in order.OrderItems)
        {
            var dailyMenuId = await _menus.NoTrackingQuery()
                .Where(dm => dm.FoodItemID == item.FoodItemID && dm.MenuDate.Date == order.OrderDate.Date)
                .Select(dm => dm.DailyMenuID)
                .FirstOrDefaultAsync(cancellationToken);
            if (dailyMenuId > 0)
            {
                await _inventory.ConsumeAsync(dailyMenuId, item.Quantity, cancellationToken);
            }
        }

        var deductResult = await _wallet.DeductOnDeliveryAsync(order.UserId, order.UserType.ToString(), order.TotalAmount, order.OrderID, cancellationToken);
        if (deductResult.IsFailure)
        {
            return new OrderStatusUpdateResultDto { Success = false, Message = deductResult.Error.Message };
        }

        order.Status = CanteenOrderStatus.Delivered;
        order.DeliveredDate = _clock.Now;

        await _audit.RecordAsync(
            action: "Order.Delivered",
            entityType: nameof(Order),
            entityId: order.OrderID.ToString(),
            payload: new { order.OrderNumber, order.TotalAmount },
            cancellationToken: cancellationToken);

        // Flush inventory consume + wallet deduction + status flip + audit row.
        // The TransactionBehavior commits the surrounding transaction but never
        // calls SaveChanges — without this line the delivery silently evaporates.
        await _uow.SaveChangesAsync(cancellationToken);

        // Side effect — best-effort SMS to the orderer.
        await TrySendDeliveredSmsAsync(order, cancellationToken);

        // Side effect — loyalty points (best-effort, never blocks delivery).
        await TryEarnLoyaltyAsync(order, cancellationToken);

        return new OrderStatusUpdateResultDto { Success = true, Message = "Order delivered." };
    }

    /// <summary>
    /// Award loyalty points based on tenant settings. The rule is:
    ///   points = floor(order.TotalAmount * Loyalty.EarnRate)
    ///   but only when TotalAmount >= Loyalty.MinSpendToEarn AND Loyalty.Enabled = true.
    /// Idempotency is handled by ILoyaltyService (one Earn entry per OrderId).
    /// </summary>
    private async Task TryEarnLoyaltyAsync(Order order, CancellationToken ct)
    {
        try
        {
            var enabled  = await _settings.GetBoolAsync("Loyalty.Enabled", false, ct);
            if (!enabled) return;
            var rate     = await _settings.GetDecimalAsync("Loyalty.EarnRate", 0m, ct);    // 0.10 = 10% back as points
            var minSpend = await _settings.GetDecimalAsync("Loyalty.MinSpendToEarn", 0m, ct);
            if (rate <= 0m) return;
            if (order.TotalAmount < minSpend) return;

            var points = decimal.Truncate(order.TotalAmount * rate);
            if (points <= 0m) return;

            var r = await _loyalty.EarnAsync(order.UserId, points, order.OrderID,
                $"Earn for order {order.OrderNumber}", ct);
            if (r.IsSuccess)
            {
                _logger.LogInformation("Awarded {Points} loyalty points to {User} for order {Order} (rate={Rate}).",
                    points, order.UserId, order.OrderID, rate);
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Loyalty earn failed for order {Order}", order.OrderID); }
    }

    private async Task TrySendDeliveredSmsAsync(Order order, CancellationToken ct)
    {
        try
        {
            string? phone = order.UserType == CanteenUserType.Student
                ? await _students.NoTrackingQuery().Where(s => s.ExternalId == order.UserId).Select(s => s.ContactNo).FirstOrDefaultAsync(ct)
                : await _employees.NoTrackingQuery().Where(e => e.ExternalId == order.UserId).Select(e => e.ContactNo).FirstOrDefaultAsync(ct);

            if (string.IsNullOrWhiteSpace(phone)) return;
            var body = $"Order #{order.OrderNumber} delivered (৳{order.TotalAmount:N0}). Thank you!";
            await _notifications.SendRawAsync(NotificationChannel.Sms, phone!, subject: null, body: body, cancellationToken: ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Delivered-SMS failed for order {Order}", order.OrderID); }
    }
}
