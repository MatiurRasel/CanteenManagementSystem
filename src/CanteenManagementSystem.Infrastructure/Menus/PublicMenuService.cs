// =============================================================================
// PublicMenuService  (CanteenManagementSystem.Infrastructure.Menus)
// -----------------------------------------------------------------------------
// IPublicMenuService default impl. Read-only — uses IReadOnlyRepository.
// =============================================================================

using CanteenManagementSystem.Application.Menus;
using CanteenManagementSystem.Domain.Menu;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Persistence;

namespace CanteenManagementSystem.Infrastructure.Menus;

public sealed class PublicMenuService : IPublicMenuService
{
    private readonly IReadOnlyRepository<DailyMenu> _menus;
    public PublicMenuService(IReadOnlyRepository<DailyMenu> menus) => _menus = menus;

    public async Task<IReadOnlyList<PublicMenuItem>> GetTodayMenuAsync(DateTime today, CancellationToken ct = default)
    {
        var rows = await _menus.NoTrackingQuery()
            .Include(dm => dm.FoodItem)
            .Where(dm => dm.MenuDate.Date == today.Date && dm.IsAvailable)
            .OrderBy(dm => dm.DisplayOrder)
            .ToListAsync(ct);

        return rows.Select((dm, i) => new PublicMenuItem(
            DailyMenuId:       dm.DailyMenuID,
            FoodItemId:        dm.FoodItemID,
            ItemNumber:        i + 1,
            ItemName:          dm.FoodItem.ItemName,
            Price:             dm.FoodItem.Price,
            Category:          dm.FoodItem.Category,
            AvailableQuantity: dm.AvailableQuantity,
            IsAvailable:       dm.IsAvailable && dm.AvailableQuantity > 0,
            ImageUrl:          dm.FoodItem.ImageUrl ?? string.Empty,
            Description:       dm.FoodItem.Description ?? string.Empty)
        {
            IsVegetarian = dm.FoodItem.IsVegetarian,
            IsHalal      = dm.FoodItem.IsHalal,
            KCalories    = dm.FoodItem.KCalories,
            Allergens    = dm.FoodItem.Allergens ?? string.Empty
        }).ToList();
    }
}
