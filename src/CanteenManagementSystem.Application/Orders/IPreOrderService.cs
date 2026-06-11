// =============================================================================
// IPreOrderService  (CanteenManagementSystem.Application.Orders)
// -----------------------------------------------------------------------------
// Pre-order / order-ahead flow. Used by /pre-orders and /admin/pre-orders/queue
// (ADR 0004).
// =============================================================================

using CanteenManagementSystem.Application.Orders.Dtos;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Menu;
using CanteenManagementSystem.Domain.Orders;

namespace CanteenManagementSystem.Application.Orders;

public sealed record PreOrderRow(int OrderId, string OrderNumber, DateTime PickupAtUtc,
    CanteenOrderStatus Status, decimal TotalAmount, IReadOnlyList<string> Items);

public interface IPreOrderService
{
    Task<string?> ResolveLinkedPersonIdAsync(int userId, CancellationToken ct = default);
    Task<CanteenUserType> ResolveUserTypeAsync(string externalId, CancellationToken ct = default);

    Task<IReadOnlyList<PreOrderRow>> ListMyUpcomingAsync(string linkedPersonId, CancellationToken ct = default);
    Task<IReadOnlyList<DailyMenu>> GetMenuForDateAsync(DateTime date, CancellationToken ct = default);

    Task<IReadOnlyList<OrderItemRequestDto>> ResolveLineItemsAsync(IReadOnlyList<int> dailyMenuIds, IReadOnlyList<int> quantities, CancellationToken ct = default);
    Task StampPreOrderMetadataAsync(int orderId, DateTime pickupAtUtc, CancellationToken ct = default);

    Task<IReadOnlyList<Order>> GetTodayPickupQueueAsync(CancellationToken ct = default);
}
