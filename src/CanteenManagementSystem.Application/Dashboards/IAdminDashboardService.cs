// =============================================================================
// IAdminDashboardService  (CanteenManagementSystem.Application.Dashboards)
// -----------------------------------------------------------------------------
// Read-only aggregates that feed the /admin landing page. One trip, one shape.
// =============================================================================

namespace CanteenManagementSystem.Application.Dashboards;

public sealed record AdminDashboardSnapshot(
    int    OrdersToday,
    int    OrdersDelivered,
    int    OrdersPending,
    decimal RevenueToday,
    decimal RevenueYesterday,
    int    ActiveStudents,
    int    ActiveEmployees,
    int    LowStockCount,
    IReadOnlyList<AdminDashboardOrderRow>  RecentOrders,
    IReadOnlyList<AdminDashboardPopularRow> PopularItems);

public sealed record AdminDashboardOrderRow(
    string  OrderNumber, string UserId, decimal TotalAmount,
    string  Status, DateTime OrderDate);

public sealed record AdminDashboardPopularRow(
    string ItemName, int OrderCount, decimal Revenue);

public interface IAdminDashboardService
{
    Task<AdminDashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
