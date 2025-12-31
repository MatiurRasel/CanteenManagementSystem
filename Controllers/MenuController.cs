using CanteenManagementSystem.Data;
using CanteenManagementSystem.Models;
using CanteenManagementSystem.Models.ViewModels;
using CanteenManagementSystem.Helpers; // Add this
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace CanteenManagementSystem.Controllers
{
    public class MenuController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MenuController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Helper method to parse meal type
        private CanteenMealType ParseMealType(string mealType)
        {
            if (Enum.TryParse<CanteenMealType>(mealType, true, out var result))
            {
                return result;
            }

            // Handle Bengali text
            return mealType switch
            {
                "সকালের নাস্তা" or "breakfast" or "Breakfast" => CanteenMealType.Breakfast,
                "লাঞ্চ" or "lunch" or "Lunch" => CanteenMealType.Lunch,
                "স্ন্যাক্স" or "snacks" or "Snacks" => CanteenMealType.Snacks,
                "ড্রিংকস" or "drinks" or "Drinks" => CanteenMealType.Drinks,
                "ডিনার" or "dinner" or "Dinner" => CanteenMealType.Dinner,
                _ => CanteenMealType.Lunch // Default
            };
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
                    Category = MealTypeHelper.GetMealTypeName(dm.FoodItem.Category), // Use helper
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
                    MealType = ParseMealType(request.MealType),
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
                    MealType = ParseMealType(request.MealType),
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
                            MealType = template.MealType ?? CanteenMealType.Lunch, // Handle nullable
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
                    MealType = t.MealType.HasValue ? MealTypeHelper.GetMealTypeName(t.MealType.Value) : "অন্যান্য", // Handle nullable
                    t.DefaultQuantity,
                    t.DisplayOrder
                })
                .ToListAsync();

            return Json(new { success = true, templates });
        }

        // Get food item by ID
        [HttpGet]
        public async Task<IActionResult> GetFoodItem(int id)
        {
            try
            {
                var foodItem = await _context.FoodItems.FindAsync(id);
                if (foodItem == null)
                {
                    return Json(new { success = false, message = "খাবার আইটেম খুঁজে পাওয়া যায়নি" });
                }

                return Json(new
                {
                    success = true,
                    item = new
                    {
                        foodItem.FoodItemID,
                        foodItem.ItemName,
                        foodItem.Description,
                        foodItem.Price,
                        Category = foodItem.Category.ToString(), // Convert enum to string
                        foodItem.ImageUrl,
                        foodItem.IsAvailable
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "একটি ত্রুটি ঘটেছে: " + ex.Message });
            }
        }

        // Create food item
        [HttpPost]
        public async Task<IActionResult> CreateFoodItem([FromBody] CreateFoodItemRequest request)
        {
            try
            {
                var foodItem = new FoodItem
                {
                    ItemName = request.ItemName,
                    Description = request.Description,
                    Price = request.Price,
                    Category = ParseMealType(request.Category),
                    ImageUrl = request.ImageUrl,
                    IsAvailable = request.IsAvailable,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                };

                _context.FoodItems.Add(foodItem);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "খাবার আইটেম তৈরি হয়েছে", id = foodItem.FoodItemID });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "একটি ত্রুটি ঘটেছে: " + ex.Message });
            }
        }

        // Update food item
        [HttpPost]
        public async Task<IActionResult> UpdateFoodItem([FromBody] UpdateFoodItemRequest request)
        {
            try
            {
                var foodItem = await _context.FoodItems.FindAsync(request.FoodItemId);
                if (foodItem == null)
                {
                    return Json(new { success = false, message = "খাবার আইটেম খুঁজে পাওয়া যায়নি" });
                }

                foodItem.ItemName = request.ItemName;
                foodItem.Description = request.Description;
                foodItem.Price = request.Price;
                foodItem.Category = ParseMealType(request.Category);
                foodItem.ImageUrl = request.ImageUrl;
                foodItem.IsAvailable = request.IsAvailable;

                _context.FoodItems.Update(foodItem);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "খাবার আইটেম আপডেট হয়েছে" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "একটি ত্রুটি ঘটেছে: " + ex.Message });
            }
        }

        // Toggle food item status
        [HttpPost]
        public async Task<IActionResult> ToggleFoodItemStatus(int id)
        {
            try
            {
                var foodItem = await _context.FoodItems.FindAsync(id);
                if (foodItem == null)
                {
                    return Json(new { success = false, message = "খাবার আইটেম খুঁজে পাওয়া যায়নি" });
                }

                foodItem.IsAvailable = !foodItem.IsAvailable;
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = foodItem.IsAvailable ? "খাবার সক্রিয় করা হয়েছে" : "খাবার নিষ্ক্রিয় করা হয়েছে",
                    isAvailable = foodItem.IsAvailable
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "একটি ত্রুটি ঘটেছে: " + ex.Message });
            }
        }

        // Get menu preview
        [HttpGet]
        public async Task<IActionResult> GetMenuPreview(DateTime date)
        {
            try
            {
                var menuItems = await _context.DailyMenus
                    .Include(dm => dm.FoodItem)
                    .Where(dm => dm.MenuDate.Date == date.Date)
                    .Select(dm => new
                    {
                        dm.DailyMenuID,
                        itemName = dm.FoodItem.ItemName,
                        dm.AvailableQuantity,
                        dm.IsAvailable
                    })
                    .ToListAsync();

                return Json(new { success = true, items = menuItems });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "একটি ত্রুটি ঘটেছে: " + ex.Message });
            }
        }

        // Quick apply template
        [HttpPost]
        public async Task<IActionResult> QuickApplyTemplate([FromBody] QuickApplyTemplateRequest request)
        {
            try
            {
                var startDate = request.StartDate;
                var templates = new List<WeeklyMenuTemplate>();

                switch (request.TemplateType)
                {
                    case "week":
                        templates = await _context.WeeklyMenuTemplates
                            .Include(t => t.FoodItem)
                            .Where(t => t.IsActive)
                            .ToListAsync();
                        break;
                    case "weekend":
                        templates = await _context.WeeklyMenuTemplates
                            .Include(t => t.FoodItem)
                            .Where(t => t.IsActive && (t.DayOfWeek == 5 || t.DayOfWeek == 6))
                            .ToListAsync();
                        break;
                    case "weekdays":
                        templates = await _context.WeeklyMenuTemplates
                            .Include(t => t.FoodItem)
                            .Where(t => t.IsActive && t.DayOfWeek >= 0 && t.DayOfWeek <= 4)
                            .ToListAsync();
                        break;
                }

                if (!templates.Any())
                {
                    return Json(new { success = false, message = "কোনো টেমপ্লেট পাওয়া যায়নি" });
                }

                var menuItem = new DailyMenu
                {
                    FoodItemID = templates.First().FoodItemID,
                    MenuDate = startDate,
                    MealType = templates.First().MealType ?? CanteenMealType.Lunch,
                    AvailableQuantity = templates.First().DefaultQuantity,
                    InitialQuantity = templates.First().DefaultQuantity,
                    IsAvailable = true,
                    DisplayOrder = templates.First().DisplayOrder
                };

                _context.DailyMenus.Add(menuItem);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "টেমপ্লেট প্রয়োগ করা হয়েছে" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "একটি ত্রুটি ঘটেছে: " + ex.Message });
            }
        }

        // Apply day template
        [HttpPost]
        public async Task<IActionResult> ApplyDayTemplate(int dayOfWeek)
        {
            try
            {
                var templates = await _context.WeeklyMenuTemplates
                    .Include(t => t.FoodItem)
                    .Where(t => t.IsActive && t.DayOfWeek == dayOfWeek)
                    .ToListAsync();

                if (!templates.Any())
                {
                    return Json(new { success = false, message = "এই দিনের জন্য কোনো টেমপ্লেট নেই" });
                }

                var today = DateTime.Today;
                var menusToAdd = new List<DailyMenu>();

                foreach (var template in templates)
                {
                    var existing = await _context.DailyMenus
                        .AnyAsync(dm => dm.MenuDate.Date == today && dm.FoodItemID == template.FoodItemID);

                    if (!existing)
                    {
                        menusToAdd.Add(new DailyMenu
                        {
                            FoodItemID = template.FoodItemID,
                            MenuDate = today,
                            MealType = template.MealType ?? CanteenMealType.Lunch,
                            AvailableQuantity = template.DefaultQuantity,
                            InitialQuantity = template.DefaultQuantity,
                            IsAvailable = true,
                            DisplayOrder = template.DisplayOrder
                        });
                    }
                }

                if (menusToAdd.Any())
                {
                    _context.DailyMenus.AddRange(menusToAdd);
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true, message = $"{menusToAdd.Count} টি আইটেম যোগ করা হয়েছে" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "একটি ত্রুটি ঘটেছে: " + ex.Message });
            }
        }

        // Batch update quantities
        [HttpPost]
        public async Task<IActionResult> BatchUpdateQuantities([FromBody] BatchUpdateRequest request)
        {
            try
            {
                var menuItems = await _context.DailyMenus
                    .Where(dm => dm.MenuDate.Date == request.MenuDate.Date)
                    .ToListAsync();

                foreach (var item in menuItems)
                {
                    var orderedQty = item.InitialQuantity - item.AvailableQuantity;
                    if (request.NewQuantity >= orderedQty)
                    {
                        item.InitialQuantity = request.NewQuantity;
                        item.AvailableQuantity = request.NewQuantity - orderedQty;
                        item.IsAvailable = item.AvailableQuantity > 0;
                    }
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true, updated = menuItems.Count, message = "পরিমাণ আপডেট করা হয়েছে" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "একটি ত্রুটি ঘটেছে: " + ex.Message });
            }
        }

        // Batch toggle availability
        [HttpPost]
        public async Task<IActionResult> BatchToggleAvailability([FromBody] BatchToggleRequest request)
        {
            try
            {
                var menuItems = await _context.DailyMenus
                    .Where(dm => dm.MenuDate.Date == request.MenuDate.Date)
                    .ToListAsync();

                foreach (var item in menuItems)
                {
                    item.IsAvailable = !item.IsAvailable;
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true, updated = menuItems.Count, message = "উপলব্ধতা পরিবর্তন করা হয়েছে" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "একটি ত্রুটি ঘটেছে: " + ex.Message });
            }
        }

        // Clear today's menu
        [HttpPost]
        public async Task<IActionResult> ClearTodayMenu()
        {
            try
            {
                var todayMenu = await _context.DailyMenus
                    .Where(dm => dm.MenuDate.Date == DateTime.Today)
                    .ToListAsync();

                _context.DailyMenus.RemoveRange(todayMenu);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "আজকের মেনু পরিষ্কার করা হয়েছে" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "একটি ত্রুটি ঘটেছে: " + ex.Message });
            }
        }

        // Create bulk template
        [HttpPost]
        public async Task<IActionResult> CreateBulkTemplate([FromBody] BulkTemplateRequest request)
        {
            try
            {
                var templates = new List<WeeklyMenuTemplate>();

                foreach (var day in request.Days)
                {
                    foreach (var foodItemId in request.FoodItemIds)
                    {
                        var template = new WeeklyMenuTemplate
                        {
                            TemplateName = $"Bulk Template - Day {day}",
                            DayOfWeek = day,
                            FoodItemID = foodItemId,
                            MealType = ParseMealType(request.MealType),
                            DefaultQuantity = request.DefaultQuantity,
                            DisplayOrder = 0,
                            IsActive = true,
                            CreatedDate = DateTime.Now
                        };

                        templates.Add(template);
                    }
                }

                _context.WeeklyMenuTemplates.AddRange(templates);
                await _context.SaveChangesAsync();

                return Json(new { success = true, count = templates.Count, message = "টেমপ্লেট তৈরি হয়েছে" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "একটি ত্রুটি ঘটেছে: " + ex.Message });
            }
        }

        // Export templates
        [HttpGet]
        public async Task<IActionResult> ExportTemplates()
        {
            try
            {
                var templates = await _context.WeeklyMenuTemplates
                    .Include(t => t.FoodItem)
                    .Where(t => t.IsActive)
                    .Select(t => new
                    {
                        t.TemplateID,
                        t.TemplateName,
                        t.DayOfWeek,
                        FoodItem = t.FoodItem.ItemName,
                        MealType = t.MealType.HasValue ? MealTypeHelper.GetMealTypeName(t.MealType.Value) : "অন্যান্য", // Handle nullable
                        t.DefaultQuantity,
                        t.DisplayOrder
                    })
                    .ToListAsync();

                return Json(new { success = true, templates });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "একটি ত্রুটি ঘটেছে: " + ex.Message });
            }
        }

        // Generate weekly report
        [HttpGet]
        public async Task<IActionResult> GenerateWeeklyReport()
        {
            try
            {
                var startDate = DateTime.Today.AddDays(-7);
                var endDate = DateTime.Today;

                var orders = await _context.Orders
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.FoodItem)
                    .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate)
                    .ToListAsync();

                // Generate CSV report
                var csv = new StringBuilder();
                csv.AppendLine("তারিখ,খাবারের নাম,পরিমাণ,মোট বিক্রি,আয়");

                foreach (var order in orders)
                {
                    foreach (var item in order.OrderItems)
                    {
                        csv.AppendLine($"{order.OrderDate:yyyy-MM-dd},{item.FoodItem.ItemName},{item.Quantity},{item.TotalPrice},{item.UnitPrice}");
                    }
                }

                return Json(new { success = true, report = csv.ToString() });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "একটি ত্রুটি ঘটেছে: " + ex.Message });
            }
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
        public string MealType { get; set; } = string.Empty; // Change from int? to string
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
        public string MealType { get; set; } = string.Empty; // Change from int? to string
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

    public class CreateFoodItemRequest
    {
        public string ItemName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public string Category { get; set; } = string.Empty; // Change from CanteenMealType to string
        public string? ImageUrl { get; set; }
        public bool IsAvailable { get; set; }
    }

    public class UpdateFoodItemRequest : CreateFoodItemRequest
    {
        public int FoodItemId { get; set; }
    }

    public class QuickApplyTemplateRequest
    {
        public string TemplateType { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
    }

    public class BatchUpdateRequest
    {
        public int NewQuantity { get; set; }
        public DateTime MenuDate { get; set; }
    }

    public class BatchToggleRequest
    {
        public DateTime MenuDate { get; set; }
    }

    public class BulkTemplateRequest
    {
        public List<int> Days { get; set; } = new();
        public List<int> FoodItemIds { get; set; } = new();
        public string MealType { get; set; } = string.Empty; // Change from CanteenMealType to string
        public int DefaultQuantity { get; set; }
    }
}