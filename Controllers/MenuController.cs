using CanteenManagementSystem.Data;
using CanteenManagementSystem.Models;
using CanteenManagementSystem.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CanteenManagementSystem.Controllers
{
    public class MenuController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MenuController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Main menu management page
        public async Task<IActionResult> Manage(DateTime? date)
        {
            var targetDate = date ?? DateTime.Today;

            var viewModel = new MenuManagementViewModel
            {
                TargetDate = targetDate,
                MenuItems = await GetDailyMenuItems(targetDate),
                AllFoodItems = await _context.FoodItems.Where(f => f.IsActive).ToListAsync(),
                WeeklyTemplates = await _context.WeeklyMenuTemplates
                    .Include(t => t.FoodItem)
                    .Where(t => t.IsActive)
                    .OrderBy(t => t.DayOfWeek)
                    .ThenBy(t => t.DisplayOrder)
                    .ToListAsync()
            };

            return View(viewModel);
        }

        private async Task<List<DailyMenuItemViewModel>> GetDailyMenuItems(DateTime date)
        {
            return await _context.DailyMenus
                .Include(dm => dm.FoodItem)
                .Where(dm => dm.MenuDate.Date == date.Date)
                .OrderBy(dm => dm.DisplayOrder)
                .Select(dm => new DailyMenuItemViewModel
                {
                    DailyMenuID = dm.DailyMenuID,
                    FoodItemID = dm.FoodItemID,
                    ItemName = dm.FoodItem.ItemName,
                    Price = dm.FoodItem.Price,
                    Category = dm.FoodItem.Category,
                    AvailableQuantity = dm.AvailableQuantity,
                    InitialQuantity = dm.InitialQuantity,
                    OrderedQuantity = dm.InitialQuantity - dm.AvailableQuantity,
                    IsAvailable = dm.IsAvailable
                })
                .ToListAsync();
        }

        // Add single item to menu 
        [HttpPost]
        public async Task<IActionResult> AddMenuItem([FromBody] AddMenuItemRequest request)
        {
            try
            {
                var existingItem = await _context.DailyMenus
                    .FirstOrDefaultAsync(dm =>
                        dm.MenuDate.Date == request.MenuDate.Date &&
                        dm.FoodItemID == request.FoodItemId);

                if (existingItem != null)
                {
                    return Json(new { success = false, message = "এই আইটেমটি ইতিমধ্যে মেনুতে আছে" });
                }

                var menuItem = new DailyMenu
                {
                    FoodItemID = request.FoodItemId,
                    MenuDate = request.MenuDate.Date,
                    MealType = (CanteenMealType)request.MealType,
                    AvailableQuantity = request.Quantity,
                    InitialQuantity = request.Quantity,
                    IsAvailable = true,
                    DisplayOrder = request.DisplayOrder
                };

                _context.DailyMenus.Add(menuItem);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "মেনুতে যোগ করা হয়েছে", menuId = menuItem.DailyMenuID });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "একটি ত্রুটি ঘটেছে: " + ex.Message });
            }
        }

        // Remove item from menu
        [HttpPost]
        public async Task<IActionResult> RemoveMenuItem(int menuId)
        {
            try
            {
                var menuItem = await _context.DailyMenus
                    .Include(dm => dm.FoodItem)
                    .FirstOrDefaultAsync(dm => dm.DailyMenuID == menuId);

                if (menuItem == null)
                {
                    return Json(new { success = false, message = "আইটেম খুঁজে পাওয়া যায়নি" });
                }

                // Check if any orders exist for this item
                var hasOrders = await _context.OrderItems
                    .AnyAsync(oi => oi.FoodItemID == menuItem.FoodItemID &&
                                   oi.Order.OrderDate.Date == menuItem.MenuDate.Date);

                if (hasOrders)
                {
                    return Json(new { success = false, message = "এই আইটেমের অর্ডার আছে, মুছে ফেলা যাবে না" });
                }

                _context.DailyMenus.Remove(menuItem);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "মেনু থেকে সরানো হয়েছে" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "একটি ত্রুটি ঘটেছে: " + ex.Message });
            }
        }

        // Update menu item quantity
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity([FromBody] UpdateQuantityRequest request)
        {
            try
            {
                var menuItem = await _context.DailyMenus.FindAsync(request.MenuId);

                if (menuItem == null)
                {
                    return Json(new { success = false, message = "আইটেম খুঁজে পাওয়া যায়নি" });
                }

                var orderedQty = menuItem.InitialQuantity - menuItem.AvailableQuantity;

                if (request.NewQuantity < orderedQty)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"ইতিমধ্যে {orderedQty} টি অর্ডার হয়েছে। কম পরিমাণ সেট করা যাবে না।"
                    });
                }

                menuItem.InitialQuantity = request.NewQuantity;
                menuItem.AvailableQuantity = request.NewQuantity - orderedQty;
                menuItem.IsAvailable = menuItem.AvailableQuantity > 0;

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "পরিমাণ আপডেট হয়েছে",
                    availableQuantity = menuItem.AvailableQuantity,
                    initialQuantity = menuItem.InitialQuantity
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "একটি ত্রুটি ঘটেছে: " + ex.Message });
            }
        }

        // Toggle menu item availability
        [HttpPost]
        public async Task<IActionResult> ToggleAvailability(int menuId)
        {
            try
            {
                var menuItem = await _context.DailyMenus.FindAsync(menuId);

                if (menuItem == null)
                {
                    return Json(new { success = false, message = "আইটেম খুঁজে পাওয়া যায়নি" });
                }

                menuItem.IsAvailable = !menuItem.IsAvailable;
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    isAvailable = menuItem.IsAvailable,
                    message = menuItem.IsAvailable ? "উপলব্ধ করা হয়েছে" : "অনুপলব্ধ করা হয়েছে"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "একটি ত্রুটি ঘটেছে: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RemoveFromMenu(int menuId)
        {
            var menuItem = await _context.DailyMenus.FindAsync(menuId);
            if (menuItem == null)
                return Json(new { success = false });

            _context.DailyMenus.Remove(menuItem);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
        

        // Food Items Management
        public async Task<IActionResult> ManageItems()
        {
            var items = await _context.FoodItems.ToListAsync();
            return View(items);
        }

        [HttpPost]
        public async Task<IActionResult> CreateItem([FromBody] FoodItem item)
        {
            try
            {
                item.IsActive = true;
                _context.FoodItems.Add(item);
                await _context.SaveChangesAsync();
                return Json(new { success = true, id = item.FoodItemID });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "একটি ত্রুটি ঘটেছে: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateItem([FromBody] FoodItem item)
        {
            try
            {
                _context.FoodItems.Update(item);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "একটি ত্রুটি ঘটেছে: " + ex.Message });
            }
        }

        // ========== WEEKLY TEMPLATE MANAGEMENT ==========

        // Create weekly template
        [HttpPost]
        public async Task<IActionResult> CreateWeeklyTemplate([FromBody] CreateTemplateRequest request)
        {
            try
            {
                var template = new WeeklyMenuTemplate
                {
                    TemplateName = request.TemplateName,
                    DayOfWeek = request.DayOfWeek,
                    FoodItemID = request.FoodItemId,
                    MealType = (CanteenMealType)request.MealType,
                    DefaultQuantity = request.DefaultQuantity,
                    DisplayOrder = request.DisplayOrder,
                    IsActive = true
                };

                _context.WeeklyMenuTemplates.Add(template);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "টেমপ্লেট তৈরি হয়েছে" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "একটি ত্রুটি ঘটেছে: " + ex.Message });
            }
        }

        // Delete weekly template
        [HttpPost]
        public async Task<IActionResult> DeleteWeeklyTemplate(int templateId)
        {
            try
            {
                var template = await _context.WeeklyMenuTemplates.FindAsync(templateId);

                if (template == null)
                {
                    return Json(new { success = false, message = "টেমপ্লেট খুঁজে পাওয়া যায়নি" });
                }

                _context.WeeklyMenuTemplates.Remove(template);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "টেমপ্লেট মুছে ফেলা হয়েছে" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "একটি ত্রুটি ঘটেছে: " + ex.Message });
            }
        }


        // Apply weekly template to dates
        [HttpPost]
        public async Task<IActionResult> ApplyWeeklyTemplate([FromBody] ApplyWeeklyTemplateRequest request)
        {
            try
            {
                var startDate = request.StartDate.Date;
                var endDate = startDate.AddDays(request.DurationDays);

                // Get all active templates
                var templates = await _context.WeeklyMenuTemplates
                    .Include(t => t.FoodItem)
                    .Where(t => t.IsActive)
                    .ToListAsync();

                if (!templates.Any())
                {
                    return Json(new { success = false, message = "কোনো টেমপ্লেট পাওয়া যায়নি" });
                }

                var menusToAdd = new List<DailyMenu>();
                var currentDate = startDate;

                while (currentDate < endDate)
                {
                    var dayOfWeek = (int)currentDate.DayOfWeek;

                    // Get templates for this day
                    var dayTemplates = templates.Where(t => t.DayOfWeek == dayOfWeek).ToList();

                    // Check if menu already exists for this date
                    var existingMenuIds = await _context.DailyMenus
                        .Where(dm => dm.MenuDate.Date == currentDate)
                        .Select(dm => dm.FoodItemID)
                        .ToListAsync();

                    foreach (var template in dayTemplates)
                    {
                        // Skip if item already in menu for this date
                        if (existingMenuIds.Contains(template.FoodItemID))
                            continue;

                        menusToAdd.Add(new DailyMenu
                        {
                            FoodItemID = template.FoodItemID,
                            MenuDate = currentDate,
                            MealType = template.MealType,
                            AvailableQuantity = template.DefaultQuantity,
                            InitialQuantity = template.DefaultQuantity,
                            IsAvailable = true,
                            DisplayOrder = template.DisplayOrder
                        });
                    }

                    currentDate = currentDate.AddDays(1);
                }

                if (menusToAdd.Any())
                {
                    _context.DailyMenus.AddRange(menusToAdd);
                    await _context.SaveChangesAsync();
                }

                return Json(new
                {
                    success = true,
                    message = $"{menusToAdd.Count} টি মেনু আইটেম যোগ করা হয়েছে",
                    itemsAdded = menusToAdd.Count
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "একটি ত্রুটি ঘটেছে: " + ex.Message });
            }
        }

        // Copy menu from one date to another
        [HttpPost]
        public async Task<IActionResult> CopyMenu([FromBody] CopyMenuRequest request)
        {
            try
            {
                var sourceMenu = await _context.DailyMenus
                    .Where(dm => dm.MenuDate.Date == request.SourceDate.Date)
                    .ToListAsync();

                if (!sourceMenu.Any())
                {
                    return Json(new { success = false, message = "সোর্স তারিখে কোনো মেনু নেই" });
                }

                // Check if target date already has menu
                var targetHasMenu = await _context.DailyMenus
                    .AnyAsync(dm => dm.MenuDate.Date == request.TargetDate.Date);

                if (targetHasMenu && !request.OverwriteExisting)
                {
                    return Json(new
                    {
                        success = false,
                        message = "টার্গেট তারিখে ইতিমধ্যে মেনু আছে। ওভাররাইট করতে চাইলে চেকবক্স সিলেক্ট করুন।"
                    });
                }

                if (request.OverwriteExisting)
                {
                    // Remove existing menu
                    var existingMenu = await _context.DailyMenus
                        .Where(dm => dm.MenuDate.Date == request.TargetDate.Date)
                        .ToListAsync();
                    _context.DailyMenus.RemoveRange(existingMenu);
                }

                // Create new menu items
                var newMenuItems = sourceMenu.Select(sm => new DailyMenu
                {
                    FoodItemID = sm.FoodItemID,
                    MenuDate = request.TargetDate.Date,
                    MealType = sm.MealType,
                    AvailableQuantity = sm.InitialQuantity, // Use initial quantity, not current
                    InitialQuantity = sm.InitialQuantity,
                    IsAvailable = true,
                    DisplayOrder = sm.DisplayOrder
                }).ToList();

                _context.DailyMenus.AddRange(newMenuItems);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = $"{newMenuItems.Count} টি আইটেম কপি করা হয়েছে"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "একটি ত্রুটি ঘটেছে: " + ex.Message });
            }
        }

        // Get weekly template by day
        [HttpGet]
        public async Task<IActionResult> GetWeeklyTemplate(int dayOfWeek)
        {
            var templates = await _context.WeeklyMenuTemplates
                .Include(t => t.FoodItem)
                .Where(t => t.DayOfWeek == dayOfWeek && t.IsActive)
                .OrderBy(t => t.DisplayOrder)
                .Select(t => new
                {
                    t.TemplateID,
                    t.FoodItemID,
                    itemName = t.FoodItem.ItemName,
                    price = t.FoodItem.Price,
                    t.MealType,
                    t.DefaultQuantity,
                    t.DisplayOrder
                })
                .ToListAsync();

            return Json(new { success = true, templates });
        }

        // Get Bengali day name
        private string GetBengaliDayName(int dayOfWeek)
        {
            var culture = new CultureInfo("bn-BD");
            return culture.DateTimeFormat.DayNames[dayOfWeek];
        }
    }

    // Request models
    public class AddMenuItemRequest
    {
        public int FoodItemId { get; set; }
        public DateTime MenuDate { get; set; }
        public int? MealType { get; set; }
        public int Quantity { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class UpdateQuantityRequest
    {
        public int MenuId { get; set; }
        public int NewQuantity { get; set; }
    }

    public class CreateTemplateRequest
    {
        public string TemplateName { get; set; } = string.Empty;
        public int DayOfWeek { get; set; }
        public int FoodItemId { get; set; }
        public int? MealType { get; set; } 
        public int DefaultQuantity { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class ApplyWeeklyTemplateRequest
    {
        public DateTime StartDate { get; set; }
        public int DurationDays { get; set; }
    }

    public class CopyMenuRequest
    {
        public DateTime SourceDate { get; set; }
        public DateTime TargetDate { get; set; }
        public bool OverwriteExisting { get; set; }
    }

    public class DailyMenuRequest
    {
        public int FoodItemId { get; set; }
        public DateTime MenuDate { get; set; }
        public string MealType { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
    }
}
