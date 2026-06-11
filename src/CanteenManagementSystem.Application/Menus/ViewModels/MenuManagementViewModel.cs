using CanteenManagementSystem.Domain.Menu;

namespace CanteenManagementSystem.Application.Menus.ViewModels;

public class MenuManagementViewModel
{
    public DateTime TargetDate { get; set; }
    public List<DailyMenuItemViewModel> MenuItems { get; set; } = new();
    public List<FoodItem> AllFoodItems { get; set; } = new();
    public List<WeeklyMenuTemplate> WeeklyTemplates { get; set; } = new();
}

public class DailyMenuItemViewModel
{
    public int DailyMenuID { get; set; }
    public int FoodItemID { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
    public int AvailableQuantity { get; set; }
    public int InitialQuantity { get; set; }
    public int OrderedQuantity { get; set; }
    public bool IsAvailable { get; set; }
}
