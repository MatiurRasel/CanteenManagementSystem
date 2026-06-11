// =============================================================================
// PlatformDashboardService  (CanteenManagementSystem.Infrastructure.Sysadmin)
// -----------------------------------------------------------------------------
// Cross-tenant read aggregate for /sysadmin/dashboard. Bypasses the global
// tenant filter via `IgnoreQueryFilters` because the SystemAdmin policy
// authorises seeing every tenant's data; controller is also gated by the
// admin IP allow-list middleware.
//
// All queries are AsNoTracking + projected up-front so we never materialise
// a heavyweight entity graph for a dashboard tile.
// =============================================================================

using CanteenManagementSystem.Application.Sysadmin;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Persistence;
using Platform.Domain.Directory;
using Platform.Domain.Tenancy;

namespace CanteenManagementSystem.Infrastructure.Sysadmin;

public sealed class PlatformDashboardService : IPlatformDashboardService
{
    private readonly IAppDbContext _db;

    public PlatformDashboardService(IAppDbContext db) => _db = db;

    public async Task<PlatformDashboardSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        var today    = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var dayAgo   = DateTime.UtcNow.AddHours(-24);

        var clients = await _db.Set<Client>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new { c.ClientId, c.ClientCode, c.ClientName, c.IsActive, c.DeletedAtUtc, c.CreatedAtUtc })
            .ToListAsync(ct);

        var totalTenants     = clients.Count;
        var deletedTenants   = clients.Count(c => c.DeletedAtUtc != null);
        var activeTenants    = clients.Count(c => c.IsActive && c.DeletedAtUtc == null);
        var suspendedTenants = clients.Count(c => !c.IsActive && c.DeletedAtUtc == null);

        var perTenantToday = await _db.Set<Order>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(o => o.OrderDate >= today && o.OrderDate < tomorrow
                        && o.Status != CanteenOrderStatus.Cancelled
                        && o.Status != CanteenOrderStatus.Refunded)
            .GroupBy(o => EF.Property<int>(o, "ClientId"))
            .Select(g => new { ClientId = g.Key, Orders = g.Count(), Revenue = g.Sum(x => x.TotalAmount) })
            .ToListAsync(ct);

        var ordersToday  = perTenantToday.Sum(x => x.Orders);
        var revenueToday = perTenantToday.Sum(x => x.Revenue);

        var perTenantTodayByClient = perTenantToday.ToDictionary(x => x.ClientId, x => x);

        var recentTenants = clients
            .Take(10)
            .Select(c =>
            {
                perTenantTodayByClient.TryGetValue(c.ClientId, out var t);
                return new PlatformTenantRow(
                    c.ClientId, c.ClientCode, c.ClientName, c.IsActive, c.DeletedAtUtc != null,
                    c.CreatedAtUtc,
                    t?.Orders ?? 0,
                    t?.Revenue ?? 0m);
            })
            .ToList();

        var failedRuns = await _db.Set<DirectorySyncRun>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.Status == "Failed" && r.StartedAtUtc >= dayAgo)
            .OrderByDescending(r => r.StartedAtUtc)
            .Take(20)
            .Select(r => new
            {
                ClientId = EF.Property<int>(r, "ClientId"),
                r.StartedAtUtc,
                r.Source,
                r.ErrorMessage
            })
            .ToListAsync(ct);

        var clientById = clients.ToDictionary(c => c.ClientId);

        // Per-tenant consecutive-failure count: number of recent runs in a row that failed
        // (we approximate via "failed today vs total today" — good enough for the dashboard).
        var totalsByClient = await _db.Set<DirectorySyncRun>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.StartedAtUtc >= dayAgo)
            .GroupBy(r => EF.Property<int>(r, "ClientId"))
            .Select(g => new
            {
                ClientId = g.Key,
                Failed   = g.Count(r => r.Status == "Failed"),
                Total    = g.Count()
            })
            .ToListAsync(ct);
        var failedByClient = totalsByClient.ToDictionary(x => x.ClientId, x => x);

        var syncFailures = failedRuns
            .Select(r =>
            {
                clientById.TryGetValue(r.ClientId, out var c);
                failedByClient.TryGetValue(r.ClientId, out var stats);
                return new PlatformSyncFailureRow(
                    r.ClientId,
                    c?.ClientCode ?? "?",
                    c?.ClientName ?? "(unknown)",
                    r.StartedAtUtc,
                    r.Source,
                    r.ErrorMessage,
                    stats?.Failed ?? 0);
            })
            .ToList();

        var perTenantMtd = await _db.Set<Order>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(o => o.OrderDate >= monthStart && o.OrderDate < tomorrow
                        && o.Status != CanteenOrderStatus.Cancelled
                        && o.Status != CanteenOrderStatus.Refunded)
            .GroupBy(o => EF.Property<int>(o, "ClientId"))
            .Select(g => new { ClientId = g.Key, Orders = g.Count(), Revenue = g.Sum(x => x.TotalAmount) })
            .OrderByDescending(x => x.Revenue)
            .Take(5)
            .ToListAsync(ct);

        var topMtd = perTenantMtd
            .Select(t =>
            {
                clientById.TryGetValue(t.ClientId, out var c);
                return new PlatformTopTenantRow(
                    t.ClientId,
                    c?.ClientCode ?? "?",
                    c?.ClientName ?? "(unknown)",
                    t.Orders,
                    t.Revenue);
            })
            .ToList();

        // Low-stock tenants: count of tenants with at least one DailyMenu line whose
        // available qty has fallen below the per-tenant threshold today.
        // We compute conservatively: any DailyMenu published today with AvailableQuantity <= 5
        // (matches the platform default Inventory.LowStockThreshold).
        var lowStockTenantIds = await _db.Set<CanteenManagementSystem.Domain.Menu.DailyMenu>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(dm => dm.MenuDate >= today && dm.MenuDate < tomorrow && dm.AvailableQuantity <= 5)
            .Select(dm => EF.Property<int>(dm, "ClientId"))
            .Distinct()
            .ToListAsync(ct);

        return new PlatformDashboardSnapshot(
            TotalTenants:        totalTenants,
            ActiveTenants:       activeTenants,
            SuspendedTenants:    suspendedTenants,
            DeletedTenants:      deletedTenants,
            OrdersToday:         ordersToday,
            RevenueToday:        revenueToday,
            FailedSyncsLast24h:  failedRuns.Count,
            LowStockTenants:     lowStockTenantIds.Count,
            RecentTenants:       recentTenants,
            SyncFailures:        syncFailures,
            TopTenantsMtd:       topMtd);
    }
}
