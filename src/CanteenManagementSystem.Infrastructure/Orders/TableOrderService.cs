// =============================================================================
// TableOrderService  (CanteenManagementSystem.Infrastructure.Orders)
// -----------------------------------------------------------------------------
// ITableOrderService default impl. Uses repos only — no IAppDbContext.
// =============================================================================

using CanteenManagementSystem.Application.Orders;
using CanteenManagementSystem.Application.Orders.Dtos;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Menu;
using CanteenManagementSystem.Domain.Orders;
using CanteenManagementSystem.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Persistence;

namespace CanteenManagementSystem.Infrastructure.Orders;

public sealed class TableOrderService : ITableOrderService
{
    private readonly IReadOnlyRepository<DailyMenu> _menus;
    private readonly IReadOnlyRepository<Student>   _students;
    private readonly IReadOnlyRepository<Employee>  _employees;
    private readonly IRepository<Order>             _orders;
    private readonly IUnitOfWork _uow;

    public TableOrderService(
        IReadOnlyRepository<DailyMenu> menus,
        IReadOnlyRepository<Student> students,
        IReadOnlyRepository<Employee> employees,
        IRepository<Order> orders,
        IUnitOfWork uow)
    {
        _menus = menus; _students = students; _employees = employees;
        _orders = orders; _uow = uow;
    }

    public async Task<IReadOnlyList<TableMenuItem>> GetTodayMenuAsync(DateTime today, CancellationToken ct = default)
        => await _menus.NoTrackingQuery()
            .Include(dm => dm.FoodItem)
            .Where(dm => dm.MenuDate.Date == today.Date && dm.IsAvailable)
            .OrderBy(dm => dm.DisplayOrder)
            .Select(dm => new TableMenuItem(
                dm.DailyMenuID, dm.FoodItemID, dm.FoodItem.ItemName, dm.FoodItem.Description,
                dm.FoodItem.Price, dm.FoodItem.IsVegetarian, dm.FoodItem.IsHalal,
                dm.FoodItem.Allergens, dm.FoodItem.KCalories,
                dm.AvailableQuantity))
            .ToListAsync(ct);

    public async Task<TableUserResolution?> ResolveUserAsync(string identifier, CancellationToken ct = default)
    {
        var ext = identifier.Trim();
        var student = await _students.NoTrackingQuery()
            .Where(x => x.ExternalId == ext || x.CardIdentifier == ext)
            .Select(x => x.ExternalId)
            .FirstOrDefaultAsync(ct);
        if (student is not null) return new TableUserResolution(student, CanteenUserType.Student);

        var employee = await _employees.NoTrackingQuery()
            .Where(x => x.ExternalId == ext || x.CardIdentifier == ext)
            .Select(x => x.ExternalId)
            .FirstOrDefaultAsync(ct);
        return employee is null ? null : new TableUserResolution(employee, CanteenUserType.Employee);
    }

    public async Task<IReadOnlyList<OrderItemRequestDto>> ResolveLineItemsAsync(IReadOnlyList<int> dailyMenuIds, IReadOnlyList<int> quantities, CancellationToken ct = default)
    {
        var items = new List<OrderItemRequestDto>();
        for (var i = 0; i < dailyMenuIds.Count; i++)
        {
            var qty = i < quantities.Count ? quantities[i] : 0;
            if (qty <= 0) continue;
            var dm = await _menus.NoTrackingQuery()
                .Include(x => x.FoodItem)
                .FirstOrDefaultAsync(x => x.DailyMenuID == dailyMenuIds[i], ct);
            if (dm is null) continue;
            items.Add(new OrderItemRequestDto
            {
                DailyMenuId = dm.DailyMenuID,
                FoodItemId  = dm.FoodItemID,
                Quantity    = qty
            });
        }
        return items;
    }

    public async Task StampTableNumberAsync(int orderId, string tableNumber, CancellationToken ct = default)
    {
        var order = await _orders.FirstOrDefaultAsync(o => o.OrderID == orderId, ct);
        if (order is null) return;
        order.TableNumber = tableNumber;
        _orders.Update(order);
        await _uow.SaveChangesAsync(ct);
    }
}
