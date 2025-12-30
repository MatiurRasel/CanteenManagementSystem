namespace CanteenManagementSystem.Models.ViewModels
{
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
        public CanteenMealType? Category { get; set; }
        public int AvailableQuantity { get; set; }
        public int InitialQuantity { get; set; }
        public int OrderedQuantity { get; set; }
        public bool IsAvailable { get; set; }
    }

    public class AddMenuItemRequest
    {
        public int FoodItemId { get; set; }
        public DateTime MenuDate { get; set; }
        public CanteenMealType? MealType { get; set; }
        public int Quantity { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class ApplyWeeklyTemplateRequest
    {
        public DateTime StartDate { get; set; }
        public int DurationDays { get; set; }
    }
}
