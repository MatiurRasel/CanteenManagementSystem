// =============================================================================
// DisplayController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Per ADR 0004 the controller depends on IPublicMenuService — no IAppDbContext.
// =============================================================================

using CanteenManagementSystem.Application.Menus;
using CanteenManagementSystem.Application.Tenancy;
using CanteenManagementSystem.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Platform.Application.Configuration;

namespace CanteenManagementSystem.Presentation.Controllers;

public class DisplayController : Controller
{
    private readonly IPublicMenuService _menu;
    private readonly IClientCacheService _clientCache;
    private readonly CanteenConfiguration _canteenConfig;
    private readonly ILogger<DisplayController> _logger;

    public DisplayController(
        IPublicMenuService menu,
        IClientCacheService clientCache,
        IOptions<CanteenConfiguration> canteenConfig,
        ILogger<DisplayController> logger)
    {
        _menu = menu;
        _clientCache = clientCache;
        _canteenConfig = canteenConfig.Value;
        _logger = logger;
    }

    public async Task<IActionResult> Menu()
    {
        try
        {
            ViewBag.ClientInfo = await _clientCache.GetClientInfoAsync();
            ViewBag.Config = _canteenConfig;
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading display menu");
            return View("Error");
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetTodayMenu(CancellationToken ct)
    {
        try
        {
            var items = await _menu.GetTodayMenuAsync(DateTime.Today, ct);
            var projected = items.Select(i => new
            {
                dailyMenuID       = i.DailyMenuId,
                foodItemID        = i.FoodItemId,
                itemNumber        = i.ItemNumber,
                itemName          = i.ItemName,
                price             = i.Price,
                category          = i.Category.HasValue ? (int)i.Category.Value : 0,
                categoryBengali   = GetCategoryBengali(i.Category),
                categoryEmoji     = GetCategoryEmoji(i.Category),
                availableQuantity = i.AvailableQuantity,
                isAvailable       = i.IsAvailable,
                imageUrl          = i.ImageUrl,
                description       = i.Description,
                isVegetarian     = i.IsVegetarian,
                isHalal          = i.IsHalal,
                kCalories        = i.KCalories,
                allergens        = i.Allergens
            });
            return Json(new
            {
                success = true,
                items   = projected,
                config  = new
                {
                    showPrices      = _canteenConfig.ShowPricesOnDisplay,
                    showStock       = _canteenConfig.ShowStockQuantity,
                    showNutrition   = _canteenConfig.ShowNutritionOnDisplay,
                    itemsPerPage    = _canteenConfig.ItemsPerPage,
                    refreshInterval = _canteenConfig.DisplayRefreshInterval
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading today's menu");
            return Json(new { success = false, message = "মেনু লোড করতে সমস্যা হয়েছে" });
        }
    }

    private static string GetCategoryBengali(CanteenMealType? category) => category switch
    {
        CanteenMealType.Breakfast => "নাস্তা",
        CanteenMealType.Lunch     => "দুপুরের খাবার",
        CanteenMealType.Snacks    => "স্ন্যাক্স",
        CanteenMealType.Drinks    => "পানীয়",
        CanteenMealType.Dinner    => "রাতের খাবার",
        _                         => string.Empty
    };

    private static string GetCategoryEmoji(CanteenMealType? category) => category switch
    {
        CanteenMealType.Breakfast => "🥐",
        CanteenMealType.Lunch     => "🍱",
        CanteenMealType.Snacks    => "🍪",
        CanteenMealType.Drinks    => "🥤",
        CanteenMealType.Dinner    => "🍛",
        _                         => "🍴"
    };
}
