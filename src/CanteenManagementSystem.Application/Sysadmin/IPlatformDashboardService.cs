// =============================================================================
// IPlatformDashboardService  (CanteenManagementSystem.Application.Sysadmin)
// -----------------------------------------------------------------------------
// Cross-tenant aggregate read for the SystemAdmin platform dashboard at
// /sysadmin/dashboard. Bypasses the tenant query filter using EF
// `IgnoreQueryFilters` — only callable from a `[Authorize(Policy="SystemAdmin")]`
// surface so the SecurityHeadersMiddleware + IP allow-list still apply.
//
// The dashboard answers four questions in one snapshot:
//   1. How many tenants live on the platform, and how many are active right now?
//   2. What's the total transaction volume across every tenant today?
//   3. Which tenants had a failed directory sync in the last 24 hours?
//   4. Which 5 tenants are the highest-revenue this month?
// =============================================================================

namespace CanteenManagementSystem.Application.Sysadmin;

public sealed record PlatformDashboardSnapshot(
    int TotalTenants,
    int ActiveTenants,
    int SuspendedTenants,
    int DeletedTenants,
    int OrdersToday,
    decimal RevenueToday,
    int FailedSyncsLast24h,
    int LowStockTenants,
    IReadOnlyList<PlatformTenantRow> RecentTenants,
    IReadOnlyList<PlatformSyncFailureRow> SyncFailures,
    IReadOnlyList<PlatformTopTenantRow> TopTenantsMtd);

public sealed record PlatformTenantRow(
    int ClientId,
    string ClientCode,
    string ClientName,
    bool IsActive,
    bool IsDeleted,
    DateTime CreatedAtUtc,
    int OrdersToday,
    decimal RevenueToday);

public sealed record PlatformSyncFailureRow(
    int ClientId,
    string ClientCode,
    string ClientName,
    DateTime FailedAtUtc,
    string Source,
    string? ErrorMessage,
    int ConsecutiveFailures);

public sealed record PlatformTopTenantRow(
    int ClientId,
    string ClientCode,
    string ClientName,
    int OrdersMtd,
    decimal RevenueMtd);

public interface IPlatformDashboardService
{
    Task<PlatformDashboardSnapshot> GetSnapshotAsync(CancellationToken ct = default);
}
