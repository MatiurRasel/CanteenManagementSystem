// =============================================================================
// IMeService  (CanteenManagementSystem.Application.Me)
// -----------------------------------------------------------------------------
// Self-serve queries for the signed-in user (/me/orders, /me/orders.csv).
// ADR 0004 — used by MeController which never touches IAppDbContext.
// =============================================================================

using CanteenManagementSystem.Domain.Orders;

namespace CanteenManagementSystem.Application.Me;

public sealed record OrderHistoryPage(
    string? LinkedPersonId,
    IReadOnlyList<Order> Items,
    int Page, int PageSize, int TotalCount,
    decimal? CurrentBalance, decimal SpentThisMonth);

public interface IMeService
{
    /// <summary>The LinkedPersonId stored on AppUsers for the given UserId — used to find their canteen rows.</summary>
    Task<string?> ResolveLinkedPersonIdAsync(int userId, CancellationToken ct = default);

    Task<OrderHistoryPage> GetOrderHistoryAsync(int userId, int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> ExportAllOrdersAsync(int userId, CancellationToken ct = default);
}
