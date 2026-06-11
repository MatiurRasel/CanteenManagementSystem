// =============================================================================
// MenuManagementService  (CanteenManagementSystem.Application.Menus)
// -----------------------------------------------------------------------------
// ADR 0004: uses IUnitOfWork + IRepository<T> / IReadOnlyRepository<T> for all
// data access. No IAppDbContext. SaveChanges goes through _uow.
// =============================================================================

using Platform.Application.Persistence;
using CanteenManagementSystem.Application.Menus.Dtos;
using CanteenManagementSystem.Application.Menus.ViewModels;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Menu;
using CanteenManagementSystem.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace CanteenManagementSystem.Application.Menus;

public class MenuManagementService : IMenuManagementService
{
    private readonly IUnitOfWork _uow;
    private readonly IReadOnlyRepository<Order> _orders;
    private readonly IReadOnlyRepository<OrderItem> _orderItems;

    public MenuManagementService(
        IUnitOfWork uow,
        IReadOnlyRepository<Order> orders,
        IReadOnlyRepository<OrderItem> orderItems)
    {
        _uow = uow;
        _orders = orders;
        _orderItems = orderItems;
    }

    private IRepository<DailyMenu>          Menus     => _uow.Repository<DailyMenu>();
    private IRepository<FoodItem>           Foods     => _uow.Repository<FoodItem>();
    private IRepository<WeeklyMenuTemplate> Templates => _uow.Repository<WeeklyMenuTemplate>();

    public async Task<MenuManagementViewModel> GetManageViewModelAsync(DateTime? date)
    {
        var targetDate = date ?? DateTime.Today;

        return new MenuManagementViewModel
        {
            TargetDate = targetDate,
            MenuItems = await GetDailyMenuItems(targetDate),
            AllFoodItems = await Foods.NoTrackingQuery().Where(f => f.IsActive).ToListAsync(),
            WeeklyTemplates = await Templates.NoTrackingQuery()
                .Include(t => t.FoodItem)
                .Where(t => t.IsActive)
                .OrderBy(t => t.DayOfWeek)
                .ThenBy(t => t.DisplayOrder)
                .ToListAsync()
        };
    }

    public Task<List<FoodItem>> GetManageItemsAsync()
        => Foods.NoTrackingQuery().ToListAsync();

    public async Task<MenuOperationResultDto> CreateMenuItemAsync(AddMenuItemRequestDto request)
    {
        try
        {
            var exists = await Menus.AnyAsync(dm => dm.MenuDate.Date == request.MenuDate.Date && dm.FoodItemID == request.FoodItemId);
            if (exists)
            {
                return Failure("এই আইটেমটি ইতিমধ্যে মেনুতে আছে");
            }

            var menuItem = new DailyMenu
            {
                FoodItemID = request.FoodItemId,
                MenuDate = request.MenuDate.Date,
                MealType = MealTypeFormatter.Parse(request.MealType),
                AvailableQuantity = request.Quantity,
                InitialQuantity = request.Quantity,
                IsAvailable = true,
                DisplayOrder = request.DisplayOrder
            };

            await Menus.AddAsync(menuItem);
            await _uow.SaveChangesAsync();

            return Ok("মেনুতে যোগ করা হয়েছে", id: menuItem.DailyMenuID);
        }
        catch (Exception ex)
        {
            return Failure("একটি ত্রুটি ঘটেছে: " + ex.Message);
        }
    }

    public async Task<MenuOperationResultDto> ToggleFoodItemStatusAsync(int id)
    {
        try
        {
            var foodItem = await Foods.GetByIdAsync(id);
            if (foodItem is null) return Failure("খাবার আইটেম খুঁজে পাওয়া যায়নি");

            foodItem.IsAvailable = !foodItem.IsAvailable;
            await _uow.SaveChangesAsync();

            return Ok(foodItem.IsAvailable ? "আইটেম উপলব্ধ করা হয়েছে" : "আইটেম অনুপলব্ধ করা হয়েছে", id);
        }
        catch (Exception ex)
        {
            return Failure("একটি ত্রুটি ঘটেছে: " + ex.Message);
        }
    }

    public async Task<List<MenuPreviewItemDto>> GetMenuPreviewAsync(DateTime date)
    {
        return await Menus.NoTrackingQuery()
            .Include(dm => dm.FoodItem)
            .Where(dm => dm.MenuDate.Date == date.Date)
            .Select(dm => new MenuPreviewItemDto
            {
                DailyMenuID = dm.DailyMenuID,
                ItemName = dm.FoodItem.ItemName,
                AvailableQuantity = dm.AvailableQuantity,
                IsAvailable = dm.IsAvailable
            })
            .ToListAsync();
    }

    public async Task<MenuOperationResultDto> QuickApplyTemplateAsync(QuickApplyTemplateRequestDto request)
    {
        try
        {
            var query = Templates.NoTrackingQuery().Include(t => t.FoodItem).Where(t => t.IsActive);

            query = request.TemplateType switch
            {
                "week" => query,
                "weekend" => query.Where(t => t.DayOfWeek == 5 || t.DayOfWeek == 6),
                "weekdays" => query.Where(t => t.DayOfWeek >= 0 && t.DayOfWeek <= 4),
                _ => query
            };

            var templates = await query.ToListAsync();
            if (templates.Count == 0) return Failure("কোনো টেমপ্লেট পাওয়া যায়নি");

            var first = templates[0];
            await Menus.AddAsync(new DailyMenu
            {
                FoodItemID = first.FoodItemID,
                MenuDate = request.StartDate,
                MealType = first.MealType ?? CanteenMealType.Lunch,
                AvailableQuantity = first.DefaultQuantity,
                InitialQuantity = first.DefaultQuantity,
                IsAvailable = true,
                DisplayOrder = first.DisplayOrder
            });
            await _uow.SaveChangesAsync();
            return Ok("টেমপ্লেট প্রয়োগ করা হয়েছে", itemsAdded: 1);
        }
        catch (Exception ex)
        {
            return Failure("একটি ত্রুটি ঘটেছে: " + ex.Message);
        }
    }

    public async Task<MenuOperationResultDto> ApplyDayTemplateAsync(int dayOfWeek)
    {
        try
        {
            var templates = await Templates.NoTrackingQuery()
                .Include(t => t.FoodItem)
                .Where(t => t.IsActive && t.DayOfWeek == dayOfWeek).ToListAsync();
            if (templates.Count == 0) return Failure("এই দিনের জন্য কোনো টেমপ্লেট নেই");

            var today = DateTime.Today;
            var menusToAdd = new List<DailyMenu>();
            foreach (var template in templates)
            {
                var exists = await Menus.AnyAsync(dm => dm.MenuDate.Date == today && dm.FoodItemID == template.FoodItemID);
                if (!exists)
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

            if (menusToAdd.Count > 0)
            {
                await Menus.AddRangeAsync(menusToAdd);
                await _uow.SaveChangesAsync();
            }

            return Ok($"{menusToAdd.Count} টি আইটেম যোগ করা হয়েছে", itemsAdded: menusToAdd.Count);
        }
        catch (Exception ex)
        {
            return Failure("একটি ত্রুটি ঘটেছে: " + ex.Message);
        }
    }

    public async Task<MenuOperationResultDto> BatchUpdateQuantitiesAsync(BatchUpdateRequestDto request)
    {
        try
        {
            var menuItems = await Menus.Query().Where(dm => dm.MenuDate.Date == request.MenuDate.Date).ToListAsync();
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
            await _uow.SaveChangesAsync();
            return Ok("পরিমাণ আপডেট করা হয়েছে", itemsAdded: menuItems.Count);
        }
        catch (Exception ex)
        {
            return Failure("একটি ত্রুটি ঘটেছে: " + ex.Message);
        }
    }

    public async Task<MenuOperationResultDto> BatchToggleAvailabilityAsync(BatchToggleRequestDto request)
    {
        try
        {
            var menuItems = await Menus.Query().Where(dm => dm.MenuDate.Date == request.MenuDate.Date).ToListAsync();
            foreach (var item in menuItems)
            {
                item.IsAvailable = !item.IsAvailable;
            }
            await _uow.SaveChangesAsync();
            return Ok("উপলব্ধতা টগল করা হয়েছে", itemsAdded: menuItems.Count);
        }
        catch (Exception ex)
        {
            return Failure("একটি ত্রুটি ঘটেছে: " + ex.Message);
        }
    }

    public async Task<MenuOperationResultDto> ClearTodayMenuAsync()
    {
        try
        {
            var todayMenu = await Menus.Query().Where(dm => dm.MenuDate.Date == DateTime.Today).ToListAsync();
            Menus.RemoveRange(todayMenu);
            await _uow.SaveChangesAsync();
            return Ok("আজকের মেনু ক্লিয়ার করা হয়েছে", itemsAdded: todayMenu.Count);
        }
        catch (Exception ex)
        {
            return Failure("একটি ত্রুটি ঘটেছে: " + ex.Message);
        }
    }

    public async Task<MenuOperationResultDto> CreateBulkTemplateAsync(BulkTemplateRequestDto request)
    {
        try
        {
            var templates = new List<WeeklyMenuTemplate>();
            foreach (var day in request.Days)
            foreach (var foodItemId in request.FoodItemIds)
            {
                templates.Add(new WeeklyMenuTemplate
                {
                    TemplateName = $"Bulk Template - Day {day}",
                    DayOfWeek = day,
                    FoodItemID = foodItemId,
                    MealType = MealTypeFormatter.Parse(request.MealType),
                    DefaultQuantity = request.DefaultQuantity,
                    DisplayOrder = 0,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                });
            }

            await Templates.AddRangeAsync(templates);
            await _uow.SaveChangesAsync();
            return Ok("টেমপ্লেট তৈরি হয়েছে", itemsAdded: templates.Count);
        }
        catch (Exception ex)
        {
            return Failure("একটি ত্রুটি ঘটেছে: " + ex.Message);
        }
    }

    public async Task<List<TemplateExportDto>> ExportTemplatesAsync()
    {
        return await Templates.NoTrackingQuery()
            .Include(t => t.FoodItem)
            .Where(t => t.IsActive)
            .Select(t => new TemplateExportDto
            {
                TemplateID = t.TemplateID,
                TemplateName = t.TemplateName,
                DayOfWeek = t.DayOfWeek,
                FoodItem = t.FoodItem.ItemName,
                MealType = t.MealType.HasValue ? MealTypeFormatter.ToBangla(t.MealType.Value) : "অন্যান্য",
                DefaultQuantity = t.DefaultQuantity,
                DisplayOrder = t.DisplayOrder
            })
            .ToListAsync();
    }

    public async Task<string> GenerateWeeklyReportAsync()
    {
        var startDate = DateTime.Today.AddDays(-7);
        var endDate = DateTime.Today;
        var orders = await _orders.NoTrackingQuery()
            .Include(o => o.OrderItems).ThenInclude(oi => oi.FoodItem)
            .Where(o => o.OrderDate >= startDate && o.OrderDate <= endDate).ToListAsync();

        var csv = new System.Text.StringBuilder();
        csv.AppendLine("তারিখ,খাবারের নাম,পরিমাণ,মোট মূল্য,দাম");
        foreach (var order in orders)
        foreach (var item in order.OrderItems)
        {
            csv.AppendLine($"{order.OrderDate:yyyy-MM-dd},{item.FoodItem.ItemName},{item.Quantity},{item.TotalPrice},{item.UnitPrice}");
        }
        return csv.ToString();
    }

    public async Task<MenuOperationResultDto> RemoveMenuItemAsync(int menuId)
    {
        try
        {
            var menuItem = await Menus.Query().Include(dm => dm.FoodItem).FirstOrDefaultAsync(dm => dm.DailyMenuID == menuId);
            if (menuItem is null) return Failure("আইটেম খুঁজে পাওয়া যায়নি");

            var hasOrders = await _orderItems.AnyAsync(oi =>
                oi.FoodItemID == menuItem.FoodItemID && oi.Order.OrderDate.Date == menuItem.MenuDate.Date);
            if (hasOrders) return Failure("এই আইটেমের অর্ডার আছে, তাই মুছে ফেলা যাবে না");

            Menus.Remove(menuItem);
            await _uow.SaveChangesAsync();
            return Ok("মেনু থেকে সরানো হয়েছে", id: menuId);
        }
        catch (Exception ex)
        {
            return Failure("একটি ত্রুটি ঘটেছে: " + ex.Message);
        }
    }

    public async Task<MenuOperationResultDto> UpdateQuantityAsync(UpdateQuantityRequestDto request)
    {
        try
        {
            var menuItem = await Menus.GetByIdAsync(request.MenuId);
            if (menuItem is null) return Failure("আইটেম খুঁজে পাওয়া যায়নি");

            var orderedQty = menuItem.InitialQuantity - menuItem.AvailableQuantity;
            if (request.NewQuantity < orderedQty)
            {
                return Failure($"ইতিমধ্যে {orderedQty} টি অর্ডার হয়েছে। কম পরিমাণ সেট করা যাবে না");
            }

            menuItem.InitialQuantity = request.NewQuantity;
            menuItem.AvailableQuantity = request.NewQuantity - orderedQty;
            menuItem.IsAvailable = menuItem.AvailableQuantity > 0;

            await _uow.SaveChangesAsync();
            return new MenuOperationResultDto
            {
                Success = true,
                Message = "পরিমাণ আপডেট হয়েছে",
                AvailableQuantity = menuItem.AvailableQuantity,
                InitialQuantity = menuItem.InitialQuantity,
                Id = menuItem.DailyMenuID
            };
        }
        catch (Exception ex)
        {
            return Failure("একটি ত্রুটি ঘটেছে: " + ex.Message);
        }
    }

    public async Task<MenuOperationResultDto> ToggleAvailabilityAsync(int menuId)
    {
        try
        {
            var menuItem = await Menus.GetByIdAsync(menuId);
            if (menuItem is null) return Failure("আইটেম খুঁজে পাওয়া যায়নি");

            menuItem.IsAvailable = !menuItem.IsAvailable;
            await _uow.SaveChangesAsync();

            return Ok(menuItem.IsAvailable ? "উপলব্ধ করা হয়েছে" : "অনুপলব্ধ করা হয়েছে", id: menuItem.DailyMenuID);
        }
        catch (Exception ex)
        {
            return Failure("একটি ত্রুটি ঘটেছে: " + ex.Message);
        }
    }

    public async Task<MenuOperationResultDto> RemoveFromMenuAsync(int menuId)
    {
        var menuItem = await Menus.GetByIdAsync(menuId);
        if (menuItem is null) return Failure("আইটেম খুঁজে পাওয়া যায়নি");

        Menus.Remove(menuItem);
        await _uow.SaveChangesAsync();
        return Ok("মেনু থেকে সরানো হয়েছে", id: menuId);
    }

    public async Task<MenuOperationResultDto> CreateItemAsync(CreateFoodItemRequestDto request)
    {
        try
        {
            var item = new FoodItem
            {
                ItemName = request.ItemName,
                Description = request.Description,
                Price = request.Price,
                Category = MealTypeFormatter.Parse(request.Category),
                ImageUrl = request.ImageUrl,
                IsAvailable = request.IsAvailable,
                IsActive = true
            };
            await Foods.AddAsync(item);
            await _uow.SaveChangesAsync();
            return Ok(id: item.FoodItemID);
        }
        catch (Exception ex)
        {
            return Failure("একটি ত্রুটি ঘটেছে: " + ex.Message);
        }
    }

    public async Task<MenuOperationResultDto> UpdateItemAsync(UpdateFoodItemRequestDto request)
    {
        try
        {
            var item = await Foods.GetByIdAsync(request.FoodItemId);
            if (item is null) return Failure("আইটেম খুঁজে পাওয়া যায়নি");

            item.ItemName = request.ItemName;
            item.Description = request.Description;
            item.Price = request.Price;
            item.Category = MealTypeFormatter.Parse(request.Category);
            item.ImageUrl = request.ImageUrl;
            item.IsAvailable = request.IsAvailable;

            Foods.Update(item);
            await _uow.SaveChangesAsync();
            return Ok(id: item.FoodItemID);
        }
        catch (Exception ex)
        {
            return Failure("একটি ত্রুটি ঘটেছে: " + ex.Message);
        }
    }

    public async Task<MenuOperationResultDto> CreateWeeklyTemplateAsync(CreateTemplateRequestDto request)
    {
        try
        {
            var template = new WeeklyMenuTemplate
            {
                TemplateName = request.TemplateName,
                DayOfWeek = request.DayOfWeek,
                FoodItemID = request.FoodItemId,
                MealType = MealTypeFormatter.Parse(request.MealType),
                DefaultQuantity = request.DefaultQuantity,
                DisplayOrder = request.DisplayOrder,
                IsActive = true
            };
            await Templates.AddAsync(template);
            await _uow.SaveChangesAsync();
            return Ok("টেমপ্লেট তৈরি হয়েছে", id: template.TemplateID);
        }
        catch (Exception ex)
        {
            return Failure("একটি ত্রুটি ঘটেছে: " + ex.Message);
        }
    }

    public async Task<MenuOperationResultDto> DeleteWeeklyTemplateAsync(int templateId)
    {
        try
        {
            var template = await Templates.GetByIdAsync(templateId);
            if (template is null) return Failure("টেমপ্লেট খুঁজে পাওয়া যায়নি");
            Templates.Remove(template);
            await _uow.SaveChangesAsync();
            return Ok("টেমপ্লেট মুছে ফেলা হয়েছে", id: templateId);
        }
        catch (Exception ex)
        {
            return Failure("একটি ত্রুটি ঘটেছে: " + ex.Message);
        }
    }

    public async Task<MenuOperationResultDto> ApplyWeeklyTemplateAsync(ApplyWeeklyTemplateRequestDto request)
    {
        try
        {
            var startDate = request.StartDate.Date;
            var endDate = startDate.AddDays(request.DurationDays);
            var templates = await Templates.NoTrackingQuery()
                .Include(t => t.FoodItem)
                .Where(t => t.IsActive)
                .ToListAsync();
            if (templates.Count == 0) return Failure("কোনো টেমপ্লেট পাওয়া যায়নি");

            var menusToAdd = new List<DailyMenu>();
            for (var currentDate = startDate; currentDate < endDate; currentDate = currentDate.AddDays(1))
            {
                var dayOfWeek = (int)currentDate.DayOfWeek;
                var dayTemplates = templates.Where(t => t.DayOfWeek == dayOfWeek).ToList();
                var existing = await Menus.NoTrackingQuery()
                    .Where(dm => dm.MenuDate.Date == currentDate)
                    .Select(dm => dm.FoodItemID)
                    .ToListAsync();

                foreach (var template in dayTemplates)
                {
                    if (existing.Contains(template.FoodItemID)) continue;
                    menusToAdd.Add(new DailyMenu
                    {
                        FoodItemID = template.FoodItemID,
                        MenuDate = currentDate,
                        MealType = template.MealType ?? CanteenMealType.Lunch,
                        AvailableQuantity = template.DefaultQuantity,
                        InitialQuantity = template.DefaultQuantity,
                        IsAvailable = true,
                        DisplayOrder = template.DisplayOrder
                    });
                }
            }

            if (menusToAdd.Count > 0)
            {
                await Menus.AddRangeAsync(menusToAdd);
                await _uow.SaveChangesAsync();
            }
            return Ok($"{menusToAdd.Count} টি মেনু আইটেম যোগ করা হয়েছে", itemsAdded: menusToAdd.Count);
        }
        catch (Exception ex)
        {
            return Failure("একটি ত্রুটি ঘটেছে: " + ex.Message);
        }
    }

    public async Task<MenuOperationResultDto> CopyMenuAsync(CopyMenuRequestDto request)
    {
        try
        {
            var sourceMenu = await Menus.NoTrackingQuery()
                .Where(dm => dm.MenuDate.Date == request.SourceDate.Date).ToListAsync();
            if (sourceMenu.Count == 0) return Failure("সোর্স তারিখে কোনো মেনু নেই");

            var targetDate = request.TargetDate.Date;
            if (request.OverwriteExisting)
            {
                var existingMenus = await Menus.Query().Where(dm => dm.MenuDate.Date == targetDate).ToListAsync();
                Menus.RemoveRange(existingMenus);
            }

            var copied = sourceMenu.Select(menu => new DailyMenu
            {
                FoodItemID = menu.FoodItemID,
                MenuDate = targetDate,
                MealType = menu.MealType,
                AvailableQuantity = menu.InitialQuantity,
                InitialQuantity = menu.InitialQuantity,
                IsAvailable = true,
                DisplayOrder = menu.DisplayOrder
            }).ToList();

            await Menus.AddRangeAsync(copied);
            await _uow.SaveChangesAsync();
            return Ok($"{copied.Count} টি আইটেম কপি করা হয়েছে", itemsAdded: copied.Count);
        }
        catch (Exception ex)
        {
            return Failure("একটি ত্রুটি ঘটেছে: " + ex.Message);
        }
    }

    private Task<List<DailyMenuItemViewModel>> GetDailyMenuItems(DateTime date)
        => Menus.NoTrackingQuery()
            .Include(dm => dm.FoodItem)
            .Where(dm => dm.MenuDate.Date == date.Date)
            .OrderBy(dm => dm.DisplayOrder)
            .Select(dm => new DailyMenuItemViewModel
            {
                DailyMenuID = dm.DailyMenuID,
                FoodItemID = dm.FoodItemID,
                ItemName = dm.FoodItem.ItemName,
                Price = dm.FoodItem.Price,
                Category = dm.FoodItem.Category.HasValue
                    ? MealTypeFormatter.ToBangla(dm.FoodItem.Category.Value)
                    : "অন্যান্য",
                AvailableQuantity = dm.AvailableQuantity,
                InitialQuantity = dm.InitialQuantity,
                OrderedQuantity = dm.InitialQuantity - dm.AvailableQuantity,
                IsAvailable = dm.IsAvailable
            })
            .ToListAsync();

    public async Task<IReadOnlyList<WeeklyTemplateItemDto>> GetWeeklyTemplateAsync(int dayOfWeek, CancellationToken ct = default)
        => await Templates.NoTrackingQuery()
            .Include(t => t.FoodItem)
            .Where(t => t.DayOfWeek == dayOfWeek && t.IsActive)
            .OrderBy(t => t.DisplayOrder)
            .Select(t => new WeeklyTemplateItemDto(
                t.TemplateID, t.FoodItemID, t.FoodItem.ItemName, t.FoodItem.Price,
                t.MealType.HasValue ? MealTypeFormatter.ToBangla(t.MealType.Value) : "অন্যান্য",
                t.DefaultQuantity, t.DisplayOrder))
            .ToListAsync(ct);

    public async Task<FoodItemDto?> GetFoodItemAsync(int id, CancellationToken ct = default)
    {
        var f = await Foods.GetByIdAsync(id, ct);
        return f is null ? null : new FoodItemDto(
            f.FoodItemID, f.ItemName, f.Description, f.Price,
            f.Category?.ToString() ?? string.Empty, f.ImageUrl, f.IsAvailable);
    }

    private static MenuOperationResultDto Ok(string? message = null, int id = 0, int itemsAdded = 0)
        => new() { Success = true, Message = message ?? string.Empty, Id = id, ItemsAdded = itemsAdded };

    private static MenuOperationResultDto Failure(string message)
        => new() { Success = false, Message = message };
}
