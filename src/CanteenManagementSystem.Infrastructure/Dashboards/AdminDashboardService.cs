// =============================================================================
// AdminDashboardService  (CanteenManagementSystem.Infrastructure.Dashboards)
// -----------------------------------------------------------------------------
// Implements IAdminDashboardService. ADR 0004: read-only repositories only.
// =============================================================================

using CanteenManagementSystem.Application.Dashboards;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Menu;
using CanteenManagementSystem.Domain.Orders;
using CanteenManagementSystem.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Persistence;

namespace CanteenManagementSystem.Infrastructure.Dashboards;

internal sealed class AdminDashboardService : IAdminDashboardService
{
    private readonly IReadOnlyRepository<Order>     _orders;
    private readonly IReadOnlyRepository<OrderItem> _items;
    private readonly IReadOnlyRepository<Student>   _students;
    private readonly IReadOnlyRepository<Employee>  _employees;
    private readonly IReadOnlyRepository<DailyMenu> _menus;

    public AdminDashboardService(
        IReadOnlyRepository<Order> orders,
        IReadOnlyRepository<OrderItem> items,
        IReadOnlyRepository<Student> students,
        IReadOnlyRepository<Employee> employees,
        IReadOnlyRepository<DailyMenu> menus)
    {
        _orders = orders; _items = items;
        _students = students; _employees = employees; _menus = menus;
    }

    public async Task<AdminDashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var today      = DateTime.Today;
        var yesterday  = today.AddDays(-1);
        var tomorrow   = today.AddDays(1);

        // Orders today/yesterday — group server-side via slim projection + in-memory aggregates
        // to dodge EF GroupBy translation limits (see ReportingService comment).
        var todayOrders = await _orders.NoTrackingQuery()
            .Where(o => o.OrderDate >= today && o.OrderDate < tomorrow)
            .Select(o => new { o.OrderID, o.Status, o.TotalAmount })
            .ToListAsync(cancellationToken);

        var yesterdayRevenue = await _orders.NoTrackingQuery()
            .Where(o => o.OrderDate >= yesterday && o.OrderDate < today && o.Status == CanteenOrderStatus.Delivered)
            .SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m;

        var deliveredCount = todayOrders.Count(o => o.Status == CanteenOrderStatus.Delivered);
        var pendingCount   = todayOrders.Count(o => o.Status == CanteenOrderStatus.Pending || o.Status == CanteenOrderStatus.Placed || o.Status == CanteenOrderStatus.Preparing);
        var revenueToday   = todayOrders.Where(o => o.Status == CanteenOrderStatus.Delivered).Sum(o => o.TotalAmount);

        var activeStudents  = await _students.CountAsync(s => s.IsActive, cancellationToken);
        var activeEmployees = await _employees.CountAsync(e => e.IsActive, cancellationToken);

        var defaultThreshold = 5;
        var lowStock = await _menus.NoTrackingQuery()
            .Include(dm => dm.FoodItem)
            .Where(dm => dm.MenuDate.Date == today && dm.IsAvailable)
            .Select(dm => new { dm.AvailableQuantity, dm.FoodItem.LowStockThreshold })
            .ToListAsync(cancellationToken);
        var lowStockCount = lowStock.Count(s => s.AvailableQuantity <= (s.LowStockThreshold ?? defaultThreshold));

        var recent = await _orders.NoTrackingQuery()
            .OrderByDescending(o => o.OrderDate)
            .Take(8)
            .Select(o => new AdminDashboardOrderRow(
                o.OrderNumber, o.UserId, o.TotalAmount,
                o.Status.ToString(), o.OrderDate))
            .ToListAsync(cancellationToken);

        var popular = await _items.NoTrackingQuery()
            .Include(oi => oi.FoodItem)
            .Where(oi => oi.Order.OrderDate >= today.AddDays(-7))
            .Select(oi => new { oi.FoodItem!.ItemName, oi.Quantity, oi.TotalPrice })
            .ToListAsync(cancellationToken);
        var popularGrouped = popular
            .GroupBy(p => p.ItemName)
            .Select(g => new AdminDashboardPopularRow(g.Key, g.Sum(x => x.Quantity), g.Sum(x => x.TotalPrice)))
            .OrderByDescending(p => p.OrderCount)
            .Take(5)
            .ToList();

        return new AdminDashboardSnapshot(
            OrdersToday:      todayOrders.Count,
            OrdersDelivered:  deliveredCount,
            OrdersPending:    pendingCount,
            RevenueToday:     revenueToday,
            RevenueYesterday: yesterdayRevenue,
            ActiveStudents:   activeStudents,
            ActiveEmployees:  activeEmployees,
            LowStockCount:    lowStockCount,
            RecentOrders:     recent,
            PopularItems:     popularGrouped);
    }
}
