// =============================================================================
// PreOrderService  (CanteenManagementSystem.Infrastructure.Orders)
// -----------------------------------------------------------------------------
// IPreOrderService default impl. Repos only — no IAppDbContext.
// =============================================================================

using CanteenManagementSystem.Application.Orders;
using CanteenManagementSystem.Application.Orders.Dtos;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Menu;
using CanteenManagementSystem.Domain.Orders;
using CanteenManagementSystem.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Persistence;
using Platform.Domain.Identity;

namespace CanteenManagementSystem.Infrastructure.Orders;

public sealed class PreOrderService : IPreOrderService
{
    private readonly IReadOnlyRepository<User>      _users;
    private readonly IReadOnlyRepository<Student>   _students;
    private readonly IReadOnlyRepository<DailyMenu> _menus;
    private readonly IRepository<Order>             _orders;
    private readonly IUnitOfWork _uow;

    public PreOrderService(
        IReadOnlyRepository<User> users,
        IReadOnlyRepository<Student> students,
        IReadOnlyRepository<DailyMenu> menus,
        IRepository<Order> orders,
        IUnitOfWork uow)
    {
        _users = users; _students = students; _menus = menus;
        _orders = orders; _uow = uow;
    }

    public Task<string?> ResolveLinkedPersonIdAsync(int userId, CancellationToken ct = default)
        => _users.NoTrackingQuery().IgnoreQueryFilters()
            .Where(u => u.UserId == userId)
            .Select(u => u.LinkedPersonId)
            .FirstOrDefaultAsync(ct);

    public async Task<CanteenUserType> ResolveUserTypeAsync(string externalId, CancellationToken ct = default)
    {
        var isStudent = await _students.AnyAsync(s => s.ExternalId == externalId, ct);
        return isStudent ? CanteenUserType.Student : CanteenUserType.Employee;
    }

    public async Task<IReadOnlyList<PreOrderRow>> ListMyUpcomingAsync(string linkedPersonId, CancellationToken ct = default)
        => await _orders.NoTrackingQuery()
            .Where(o => o.UserId == linkedPersonId && o.IsPreOrder)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.FoodItem)
            .OrderByDescending(o => o.PickupAtUtc)
            .Take(50)
            .Select(o => new PreOrderRow(
                o.OrderID, o.OrderNumber, o.PickupAtUtc!.Value, o.Status, o.TotalAmount,
                o.OrderItems.Select(oi => $"{oi.Quantity}× {oi.FoodItem.ItemName}").ToList()))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DailyMenu>> GetMenuForDateAsync(DateTime date, CancellationToken ct = default)
        => await _menus.NoTrackingQuery()
            .Include(dm => dm.FoodItem)
            .Where(dm => dm.MenuDate.Date == date.Date && dm.IsAvailable)
            .OrderBy(dm => dm.DisplayOrder)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<OrderItemRequestDto>> ResolveLineItemsAsync(IReadOnlyList<int> dailyMenuIds, IReadOnlyList<int> quantities, CancellationToken ct = default)
    {
        var items = new List<OrderItemRequestDto>();
        for (var i = 0; i < dailyMenuIds.Count; i++)
        {
            var qty = i < quantities.Count ? quantities[i] : 0;
            if (qty <= 0) continue;
            var dm = await _menus.NoTrackingQuery().Include(d => d.FoodItem)
                .FirstOrDefaultAsync(d => d.DailyMenuID == dailyMenuIds[i], ct);
            if (dm is null) continue;
            items.Add(new OrderItemRequestDto
            {
                FoodItemId  = dm.FoodItemID,
                DailyMenuId = dm.DailyMenuID,
                Quantity    = qty
            });
        }
        return items;
    }

    public async Task StampPreOrderMetadataAsync(int orderId, DateTime pickupAtUtc, CancellationToken ct = default)
    {
        var order = await _orders.FirstOrDefaultAsync(o => o.OrderID == orderId, ct);
        if (order is null) return;
        order.PickupAtUtc = pickupAtUtc;
        order.IsPreOrder = true;
        _orders.Update(order);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Order>> GetTodayPickupQueueAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;
        return await _orders.NoTrackingQuery()
            .Include(o => o.OrderItems).ThenInclude(oi => oi.FoodItem)
            .Where(o => o.IsPreOrder && o.PickupAtUtc != null && o.PickupAtUtc.Value.Date == today
                        && (o.Status == CanteenOrderStatus.Pending || o.Status == CanteenOrderStatus.Placed
                         || o.Status == CanteenOrderStatus.Confirmed || o.Status == CanteenOrderStatus.Preparing
                         || o.Status == CanteenOrderStatus.Ready))
            .OrderBy(o => o.OrderDate)
            .ToListAsync(ct);
    }
}
