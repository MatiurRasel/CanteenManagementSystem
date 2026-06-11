// =============================================================================
// ITableOrderService  (CanteenManagementSystem.Application.Orders)
// -----------------------------------------------------------------------------
// QR-on-table consumer flow. Used by /t/{tableNumber} (ADR 0004).
// =============================================================================

using CanteenManagementSystem.Application.Orders.Dtos;
using CanteenManagementSystem.Domain.Enums;

namespace CanteenManagementSystem.Application.Orders;

public sealed record TableMenuItem(
    int DailyMenuId, int FoodItemId, string Name, string? Description,
    decimal Price, bool IsVegetarian, bool IsHalal, string? Allergens, int? KCalories,
    int AvailableQuantity);

public sealed record TableUserResolution(string ExternalId, CanteenUserType UserType);

public interface ITableOrderService
{
    Task<IReadOnlyList<TableMenuItem>> GetTodayMenuAsync(DateTime today, CancellationToken ct = default);
    Task<TableUserResolution?> ResolveUserAsync(string identifier, CancellationToken ct = default);

    /// <summary>Resolve a list of DailyMenu rows for the (id, qty) pairs the customer picked.</summary>
    Task<IReadOnlyList<OrderItemRequestDto>> ResolveLineItemsAsync(IReadOnlyList<int> dailyMenuIds, IReadOnlyList<int> quantities, CancellationToken ct = default);

    /// <summary>Set Order.TableNumber after a successful PlaceOrderCommand. No-op if order not found.</summary>
    Task StampTableNumberAsync(int orderId, string tableNumber, CancellationToken ct = default);
}
