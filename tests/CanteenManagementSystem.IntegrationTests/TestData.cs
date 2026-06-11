// =============================================================================
// TestData  (CanteenManagementSystem.IntegrationTests)
// -----------------------------------------------------------------------------
// Arrange helpers — insert wallet/menu fixtures through the real
// ApplicationDbContext resolved from a service scope, so tenant stamping and
// rowversion behave exactly like production. Whether rows land tenant-scoped
// or unscoped depends on the scope's RequestTenantContext (see tests).
// =============================================================================

using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Menu;
using CanteenManagementSystem.Domain.Wallets;
using CanteenManagementSystem.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CanteenManagementSystem.IntegrationTests;

internal static class TestData
{
    /// <summary>Insert a food item + today's daily menu + a funded wallet for <paramref name="userId"/>.</summary>
    public static async Task<(int FoodItemId, int DailyMenuId)> ArrangeMenuAndWalletAsync(
        IServiceScope scope,
        string userId,
        string itemName,
        decimal price,
        decimal walletFunds,
        int quantity = 100,
        CanteenUserType userType = CanteenUserType.Employee)
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var food = new FoodItem
        {
            ItemName = itemName,
            Description = "integration-test item",
            Price = price,
            Category = CanteenMealType.Lunch,
            IsAvailable = true,
            IsActive = true
        };
        db.FoodItems.Add(food);
        await db.SaveChangesAsync();

        var menu = new DailyMenu
        {
            FoodItemID = food.FoodItemID,
            MenuDate = DateTime.Today,
            MealType = CanteenMealType.Lunch,
            IsAvailable = true,
            AvailableQuantity = quantity,
            InitialQuantity = quantity
        };
        db.DailyMenus.Add(menu);

        db.UserBalances.Add(new UserBalance
        {
            UserId = userId,
            UserType = userType,
            TotalBalance = walletFunds,
            UsedBalance = 0,
            BlockedAmount = 0
        });

        await db.SaveChangesAsync();
        return (food.FoodItemID, menu.DailyMenuID);
    }
}
