// =============================================================================
// ReportingService  (Infrastructure.Reports)
// -----------------------------------------------------------------------------
// Heavy-lift aggregations for the admin dashboards. Each public method:
//   1. Computes a deterministic cache key (date range + tenant via ICacheService).
//   2. Calls EF with GROUP BY -> projects directly to row records.
//   3. Caches the result for 60 seconds (CacheTtl.Short).
//
// WHY no view models in Domain
//   Reports are read-side; row records live next to the service interface to
//   keep the contract honest. They are *not* domain entities.
// =============================================================================

using System.Globalization;
using System.Text;
using Platform.Application.Persistence;
using Platform.Application.Abstractions.Caching;
using Platform.Application.Abstractions.Reports;
using Platform.Application.Caching;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace CanteenManagementSystem.Infrastructure.Reports;

// LAYERING (ADR 0004, Batch 2): a reporting service is read-only by definition,
// so we inject IReadOnlyRepository<T> for every entity type we project from.
// This makes the intent unambiguous AND prevents a future refactor accidentally
// introducing a write through the wrong service.
internal sealed class ReportingService : IReportingService
{
    private readonly IReadOnlyRepository<Order> _orders;
    private readonly IReadOnlyRepository<OrderItem> _orderItems;
    private readonly IReadOnlyRepository<Domain.Menu.DailyMenu> _menus;
    private readonly IReadOnlyRepository<Domain.Users.Employee> _employees;
    private readonly IReadOnlyRepository<Domain.Users.Student> _students;
    private readonly ICacheService _cache;

    public ReportingService(
        IReadOnlyRepository<Order> orders,
        IReadOnlyRepository<OrderItem> orderItems,
        IReadOnlyRepository<Domain.Menu.DailyMenu> menus,
        IReadOnlyRepository<Domain.Users.Employee> employees,
        IReadOnlyRepository<Domain.Users.Student> students,
        ICacheService cache)
    {
        _orders = orders;
        _orderItems = orderItems;
        _menus = menus;
        _employees = employees;
        _students = students;
        _cache = cache;
    }

    public Task<DailyCollectionReport> GetDailyCollectionAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        => _cache.GetOrSetAsync(
            $"reports:daily-collection:{fromDate:yyyy-MM-dd}:{toDate:yyyy-MM-dd}",
            async ct => await BuildDailyCollectionAsync(fromDate, toDate, ct),
            ttl: CacheTtl.Short,
            cancellationToken: cancellationToken);

    public Task<IReadOnlyList<PopularItemRow>> GetPopularItemsAsync(DateTime fromDate, DateTime toDate, int top = 10, CancellationToken cancellationToken = default)
        => _cache.GetOrSetAsync(
            $"reports:popular:{fromDate:yyyy-MM-dd}:{toDate:yyyy-MM-dd}:{top}",
            async ct =>
            {
                // Two sums in the same GroupBy projection — EF Core 10 actually
                // handles this one (no Count() + Sum() mix). Kept server-side.
                var rows = await (
                    from oi in _orderItems.NoTrackingQuery()
                    where oi.Order.OrderDate >= fromDate && oi.Order.OrderDate <= toDate
                          && oi.Order.Status == CanteenOrderStatus.Delivered
                    group oi by oi.FoodItem.ItemName into g
                    orderby g.Sum(x => x.Quantity) descending
                    select new PopularItemRow(g.Key, g.Sum(x => x.Quantity), g.Sum(x => x.TotalPrice))
                ).Take(top).ToListAsync(ct);
                return (IReadOnlyList<PopularItemRow>)rows;
            },
            ttl: CacheTtl.Short,
            cancellationToken: cancellationToken);

    public Task<WastageReport> GetWastageAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        => _cache.GetOrSetAsync(
            $"reports:wastage:{fromDate:yyyy-MM-dd}:{toDate:yyyy-MM-dd}",
            async ct =>
            {
                // EF Core 10 GroupBy projection limitation — see notes on
                // BuildDailyCollectionAsync. Pull slim rows, group in memory.
                var raw = await _menus.NoTrackingQuery()
                    .Where(dm => dm.MenuDate >= fromDate && dm.MenuDate <= toDate)
                    .Select(dm => new
                    {
                        ItemName = dm.FoodItem.ItemName,
                        dm.InitialQuantity,
                        dm.AvailableQuantity,
                        Price = dm.FoodItem.Price
                    })
                    .ToListAsync(ct);

                var rows = raw
                    .GroupBy(x => x.ItemName)
                    .Select(g => new WastageRow(
                        g.Key,
                        g.Sum(x => x.InitialQuantity),
                        g.Sum(x => x.InitialQuantity - x.AvailableQuantity),
                        g.Sum(x => x.AvailableQuantity),
                        g.Sum(x => x.AvailableQuantity * x.Price)))
                    .ToList();
                return new WastageReport(rows.Sum(r => r.WasteValue), rows);
            },
            ttl: CacheTtl.Short,
            cancellationToken: cancellationToken);

    public Task<IReadOnlyList<PeakHourRow>> GetPeakHoursAsync(DateTime targetDate, CancellationToken cancellationToken = default)
        => _cache.GetOrSetAsync(
            $"reports:peak-hour:{targetDate:yyyy-MM-dd}",
            async ct =>
            {
                // EF Core 10 chokes on Count()+Sum() in the same GroupBy projection.
                // Slim projection + in-memory grouping (max 24 buckets/day).
                var raw = await _orders.NoTrackingQuery()
                    .Where(o => o.OrderDate.Date == targetDate.Date)
                    .Select(o => new { o.OrderDate, o.TotalAmount })
                    .ToListAsync(ct);

                var rows = raw
                    .GroupBy(o => o.OrderDate.Hour)
                    .OrderBy(g => g.Key)
                    .Select(g => new PeakHourRow(g.Key, g.Count(), g.Sum(x => x.TotalAmount)))
                    .ToList();
                return (IReadOnlyList<PeakHourRow>)rows;
            },
            ttl: CacheTtl.Short,
            cancellationToken: cancellationToken);

    public Task<IReadOnlyList<DepartmentSpendRow>> GetDepartmentSpendAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
        => _cache.GetOrSetAsync(
            $"reports:dept-spend:{fromDate:yyyy-MM-dd}:{toDate:yyyy-MM-dd}",
            async ct =>
            {
                // Department comes from Employee.EmployeeType (employees) and
                // Student.Program (students) — populated by Excel/CSV import or
                // DirectorySyncService. Distinct().Count() + GroupBy can't both
                // run inside one EF projection, so fetch slim rows + group in
                // memory. Joined indexes (UserId, OrderDate) keep this fast.
                var empRaw = await (
                    from o in _orders.NoTrackingQuery()
                    join e in _employees.NoTrackingQuery() on o.UserId equals e.ExternalId
                    where o.OrderDate >= fromDate && o.OrderDate <= toDate && o.UserType == CanteenUserType.Employee
                    select new { Group = e.EmployeeType, o.UserId, o.TotalAmount }
                ).ToListAsync(ct);

                var employees = empRaw
                    .GroupBy(x => x.Group ?? "Unknown")
                    .Select(g => new DepartmentSpendRow(
                        g.Key,
                        g.Select(x => x.UserId).Distinct().Count(),
                        g.Count(),
                        g.Sum(x => x.TotalAmount)))
                    .ToList();

                var stuRaw = await (
                    from o in _orders.NoTrackingQuery()
                    join s in _students.NoTrackingQuery() on o.UserId equals s.ExternalId
                    where o.OrderDate >= fromDate && o.OrderDate <= toDate && o.UserType == CanteenUserType.Student
                    select new { Group = s.Program, o.UserId, o.TotalAmount }
                ).ToListAsync(ct);

                var students = stuRaw
                    .GroupBy(x => x.Group ?? "Unknown")
                    .Select(g => new DepartmentSpendRow(
                        g.Key,
                        g.Select(x => x.UserId).Distinct().Count(),
                        g.Count(),
                        g.Sum(x => x.TotalAmount)))
                    .ToList();

                return (IReadOnlyList<DepartmentSpendRow>)employees.Concat(students).ToList();
            },
            ttl: CacheTtl.Short,
            cancellationToken: cancellationToken);

    public async Task<byte[]> ExportDailyCollectionCsvAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var report = await GetDailyCollectionAsync(fromDate, toDate, cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine("Date,Orders,Revenue,Refunded");
        foreach (var row in report.Days)
        {
            sb.AppendLine(string.Join(',',
                row.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                row.Orders,
                row.Revenue.ToString("F2", CultureInfo.InvariantCulture),
                row.Refunded.ToString("F2", CultureInfo.InvariantCulture)));
        }
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private async Task<DailyCollectionReport> BuildDailyCollectionAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken)
    {
        // EF Core 10 can't translate GroupBy projections that contain
        // sub-aggregations (.Where(...).Sum(...)) OR conditional sums
        // (CASE WHEN ... THEN ... ELSE 0 END) — both forms fail with the
        // same "could not be translated" error. The reliable pattern is:
        //   1. Pull a slim row shape (OrderDate, Status, TotalAmount only)
        //      with the WHERE clause executed in SQL — covered by the
        //      (OrderDate, Status) index, so it's an index scan + project.
        //   2. Group + aggregate in memory. Even a 90-day window returns at
        //      most a few thousand rows; the grouping is O(n) and fast.
        // This is the same pattern EF Core team recommends in their docs:
        // https://learn.microsoft.com/ef/core/querying/sql-queries#client-vs-server-evaluation
        var raw = await _orders.NoTrackingQuery()
            .Where(o => o.OrderDate >= fromDate && o.OrderDate <= toDate)
            .Select(o => new { o.OrderDate, o.Status, o.TotalAmount })
            .ToListAsync(cancellationToken);

        var rows = raw
            .GroupBy(o => o.OrderDate.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyCollectionRow(
                g.Key,
                g.Count(),
                g.Where(o => o.Status == CanteenOrderStatus.Delivered).Sum(o => o.TotalAmount),
                g.Where(o => o.Status == CanteenOrderStatus.Refunded).Sum(o => o.TotalAmount)))
            .ToList();

        return new DailyCollectionReport(rows.Sum(r => r.Revenue), rows.Sum(r => r.Orders), rows);
    }
}
