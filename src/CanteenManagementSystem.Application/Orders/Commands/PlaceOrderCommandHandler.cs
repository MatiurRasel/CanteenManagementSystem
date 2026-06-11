// =============================================================================
// PlaceOrderCommandHandler
// -----------------------------------------------------------------------------
// ADR 0004: IUnitOfWork + IReadOnlyRepository<T>. The TransactionBehavior in
// the dispatcher pipeline wraps the whole command so a failure here rolls
// everything back, including the stock reservations made via IInventoryService.
// =============================================================================

using Platform.Application.Persistence;
using Platform.Application.Abstractions.Audit;
using Platform.Application.Abstractions.Inventory;
using Platform.Application.Abstractions.Notifications;
using Platform.Application.Abstractions.RealTime;
using Platform.Application.Abstractions.Webhooks;
using Platform.Application.Abstractions.Time;
using Platform.Application.Abstractions.Wallets;
using Platform.Application.Dispatch;
using CanteenManagementSystem.Application.Orders.Dtos;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Menu;
using CanteenManagementSystem.Domain.Orders;
using CanteenManagementSystem.Domain.Users;
using CanteenManagementSystem.Domain.Wallets;
using Platform.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanteenManagementSystem.Application.Orders.Commands;

/// Implements §6.2 of the source of truth:
///   * Block on order placement (wallet + stock both reserved, not consumed)
///   * Deduct on delivery (handled in MarkOrderDeliveredCommand)
/// Idempotent: a retry with the same IdempotencyKey returns the original order.
internal sealed class PlaceOrderCommandHandler : IRequestHandler<PlaceOrderCommand, OrderResponseDto>
{
    private readonly IUnitOfWork _uow;
    private readonly IReadOnlyRepository<UserBalance> _balances;
    private readonly IReadOnlyRepository<Student> _students;
    private readonly IReadOnlyRepository<Employee> _employees;
    private readonly IReadOnlyRepository<Platform.Domain.Identity.User> _users;
    private readonly IWalletService _wallet;
    private readonly IInventoryService _inventory;
    private readonly IAuditTrail _audit;
    private readonly IClock _clock;
    private readonly INotificationService _notifications;
    private readonly IOrderBroadcaster _broadcaster;
    private readonly IWebhookPublisher _webhooks;
    private readonly ILogger<PlaceOrderCommandHandler> _logger;

    public PlaceOrderCommandHandler(
        IUnitOfWork uow,
        IReadOnlyRepository<UserBalance> balances,
        IReadOnlyRepository<Student> students,
        IReadOnlyRepository<Employee> employees,
        IReadOnlyRepository<Platform.Domain.Identity.User> users,
        IWalletService wallet,
        IInventoryService inventory,
        IAuditTrail audit,
        IClock clock,
        INotificationService notifications,
        IOrderBroadcaster broadcaster,
        IWebhookPublisher webhooks,
        ILogger<PlaceOrderCommandHandler> logger)
    {
        _uow = uow;
        _balances = balances;
        _students = students;
        _employees = employees;
        _users = users;
        _wallet = wallet;
        _inventory = inventory;
        _audit = audit;
        _clock = clock;
        _notifications = notifications;
        _broadcaster = broadcaster;
        _webhooks = webhooks;
        _logger = logger;
    }

    public async Task<OrderResponseDto> HandleAsync(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var ordersRepo = _uow.Repository<Order>();
        var menus = _uow.Repository<DailyMenu>();

        if (!string.IsNullOrEmpty(request.IdempotencyKey))
        {
            var existing = await ordersRepo.NoTrackingQuery()
                .FirstOrDefaultAsync(o => o.IdempotencyKey == request.IdempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return new OrderResponseDto
                {
                    Success = true,
                    Message = "Duplicate request returned cached result.",
                    OrderId = existing.OrderID,
                    OrderNumber = existing.OrderNumber,
                    TotalAmount = existing.TotalAmount
                };
            }
        }

        decimal totalAmount = 0;
        var orderItems = new List<OrderItem>();

        foreach (var item in request.Items)
        {
            var dailyMenu = await menus.Query()
                .Include(dm => dm.FoodItem)
                .FirstOrDefaultAsync(dm => dm.DailyMenuID == item.DailyMenuId, cancellationToken);

            if (dailyMenu is null || !dailyMenu.IsAvailable)
            {
                return Failure("আইটেম উপলব্ধ নেই");
            }

            var reserveResult = await _inventory.ReserveAsync(item.DailyMenuId, item.Quantity, cancellationToken);
            if (reserveResult.IsFailure) return Failure(reserveResult.Error.Message);

            var itemTotal = dailyMenu.FoodItem.Price * item.Quantity;
            totalAmount += itemTotal;
            orderItems.Add(new OrderItem
            {
                FoodItemID = item.FoodItemId,
                Quantity = item.Quantity,
                UnitPrice = dailyMenu.FoodItem.Price,
                TotalPrice = itemTotal
            });
        }

        var order = new Order
        {
            OrderNumber = GenerateOrderNumber(request.UserIdentifier),
            UserId = request.UserId,
            UserType = request.UserType,
            TotalAmount = totalAmount,
            Status = CanteenOrderStatus.Placed,
            OrderDate = _clock.Now,
            OrderItems = orderItems,
            IdempotencyKey = request.IdempotencyKey
        };
        await ordersRepo.AddAsync(order, cancellationToken);

        // First save: persist the Order so it has an OrderID for the wallet block.
        await _uow.SaveChangesAsync(cancellationToken);

        var blockResult = await _wallet.BlockAsync(request.UserId, request.UserType.ToString(), totalAmount, order.OrderID, request.IdempotencyKey, cancellationToken);
        if (blockResult.IsFailure)
        {
            return Failure(blockResult.Error.Message);
        }

        await _audit.RecordAsync(
            action: "Order.Placed",
            entityType: nameof(Order),
            entityId: order.OrderID.ToString(),
            payload: new { order.OrderNumber, order.UserId, totalAmount, ItemCount = orderItems.Count },
            cancellationToken: cancellationToken);

        // Second save: flush wallet-block + audit changes before reading the balance snapshot.
        await _uow.SaveChangesAsync(cancellationToken);

        var balanceSnapshot = await _balances.FirstOrDefaultAsync(
            b => b.UserId == request.UserId && b.UserType == request.UserType, cancellationToken);

        // ─── Side effects: SignalR push + SMS to user/parent (best-effort) ───
        await TryBroadcastAndNotifyAsync(request, order, orderItems, totalAmount, cancellationToken);

        return new OrderResponseDto
        {
            Success = true,
            Message = "অর্ডার সফলভাবে গৃহীত হয়েছে",
            OrderId = order.OrderID,
            OrderNumber = order.OrderNumber,
            TotalAmount = totalAmount,
            RemainingBalance = balanceSnapshot?.AvailableBalance ?? 0
        };
    }

    /// <summary>
    /// Fan-out side effects on a successful order:
    ///   * Push the order to KitchenDisplayHub clients via IOrderBroadcaster.
    ///   * Send an SMS to the orderer's saved phone (Students → parent contact if any).
    /// All failures are LOGGED but never propagate — the order is already committed.
    /// </summary>
    private async Task TryBroadcastAndNotifyAsync(
        PlaceOrderRequestDto request, Order order,
        List<OrderItem> items, decimal totalAmount, CancellationToken ct)
    {
        try
        {
            var menuItemsForBroadcast = items.Select(i => new {
                itemName = i.FoodItem?.ItemName ?? string.Empty,
                quantity = i.Quantity,
                unitPrice = i.UnitPrice
            }).ToList();

            await _broadcaster.OrderPlacedAsync(new
            {
                orderId      = order.OrderID,
                orderNumber  = order.OrderNumber,
                userName     = order.UserId,
                items        = menuItemsForBroadcast,
                totalAmount,
                orderDateUtc = order.OrderDate.ToUniversalTime().ToString("o"),
                status       = (int)order.Status
            }, ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Order broadcast failed for #{Order}", order.OrderID); }

        try
        {
            string? phone = await ResolveContactPhoneAsync(request.UserId, request.UserType, ct);
            if (!string.IsNullOrWhiteSpace(phone))
            {
                var body = $"Order #{order.OrderNumber} placed: {items.Count} item(s), ৳{totalAmount:N0}.";
                await _notifications.SendRawAsync(NotificationChannel.Sms, phone!, subject: null, body: body, cancellationToken: ct);
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "OrderPlaced SMS failed for #{Order}", order.OrderID); }

        // Parent fan-out: for student orders, also SMS every parent account that
        // has linked this child via LinkedChildrenCsv. Best-effort, swallow errors.
        if (request.UserType == CanteenUserType.Student)
        {
            try
            {
                var parents = await _users.NoTrackingQuery()
                    .Where(u => u.LinkedChildrenCsv != null
                                && u.LinkedChildrenCsv.Contains(request.UserId)
                                && u.PhoneNumber != null
                                && u.IsActive)
                    .Select(u => new { u.UserId, u.PhoneNumber, u.LinkedChildrenCsv })
                    .ToListAsync(ct);

                foreach (var parent in parents)
                {
                    // The Contains() filter above is a substring match — guard against
                    // false positives where the searched id is a prefix/substring of
                    // a different linked id (e.g. "STD-1" matching "STD-12").
                    var linkedIds = (parent.LinkedChildrenCsv ?? "")
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (!linkedIds.Contains(request.UserId, StringComparer.OrdinalIgnoreCase)) continue;

                    var body = $"Your child placed order #{order.OrderNumber}: {items.Count} item(s), ৳{totalAmount:N0}.";
                    await _notifications.SendRawAsync(
                        NotificationChannel.Sms, parent.PhoneNumber!, subject: null, body: body, cancellationToken: ct);
                }
            }
            catch (Exception ex) { _logger.LogWarning(ex, "OrderPlaced parent-fanout failed for #{Order}", order.OrderID); }
        }

        try
        {
            await _webhooks.PublishAsync(WebhookEvents.OrderPlaced, new
            {
                orderId      = order.OrderID,
                orderNumber  = order.OrderNumber,
                userId       = order.UserId,
                userType     = order.UserType.ToString(),
                totalAmount,
                itemCount    = items.Count,
                orderDateUtc = order.OrderDate.ToUniversalTime().ToString("o"),
                tableNumber  = order.TableNumber,
                isPreOrder   = order.IsPreOrder
            }, ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "OrderPlaced webhook publish failed for #{Order}", order.OrderID); }
    }

    private async Task<string?> ResolveContactPhoneAsync(string userId, CanteenUserType userType, CancellationToken ct)
    {
        // Canteen-owned schema (SmartCanteen DB): the canonical contact lives on
        // Student / Employee. No bridge fallback — everything is created in our DB.
        if (userType == CanteenUserType.Student)
        {
            return await _students.NoTrackingQuery()
                .Where(s => s.ExternalId == userId)
                .Select(s => s.ContactNo).FirstOrDefaultAsync(ct);
        }
        return await _employees.NoTrackingQuery()
            .Where(e => e.ExternalId == userId)
            .Select(e => e.ContactNo).FirstOrDefaultAsync(ct);
    }

    private static OrderResponseDto Failure(string message)
        => new() { Success = false, Message = message };

    private static string GenerateOrderNumber(string userId)
        => $"ORD-{DateTime.Now:yyMMdd}-{userId}-{Random.Shared.Next(100, 999)}";
}
