#nullable enable

namespace CanteenManagementSystem.Application.Menus.Dtos;

public class AddMenuItemRequestDto
{
    public int FoodItemId { get; set; }
    public DateTime MenuDate { get; set; }
    public string MealType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int DisplayOrder { get; set; }
}

public class UpdateQuantityRequestDto
{
    public int MenuId { get; set; }
    public int NewQuantity { get; set; }
}

public class CreateTemplateRequestDto
{
    public string TemplateName { get; set; } = string.Empty;
    public int DayOfWeek { get; set; }
    public int FoodItemId { get; set; }
    public string MealType { get; set; } = string.Empty;
    public int DefaultQuantity { get; set; }
    public int DisplayOrder { get; set; }
}

public class ApplyWeeklyTemplateRequestDto
{
    public DateTime StartDate { get; set; }
    public int DurationDays { get; set; }
}

public class CopyMenuRequestDto
{
    public DateTime SourceDate { get; set; }
    public DateTime TargetDate { get; set; }
    public bool OverwriteExisting { get; set; }
}

public class DailyMenuRequestDto
{
    public int FoodItemId { get; set; }
    public DateTime MenuDate { get; set; }
    public string MealType { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

public class CreateFoodItemRequestDto
{
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; }
}

public class UpdateFoodItemRequestDto : CreateFoodItemRequestDto
{
    public int FoodItemId { get; set; }
}

public class QuickApplyTemplateRequestDto
{
    public string TemplateType { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
}

public class BatchUpdateRequestDto
{
    public int NewQuantity { get; set; }
    public DateTime MenuDate { get; set; }
}

public class BatchToggleRequestDto
{
    public DateTime MenuDate { get; set; }
}

public class BulkTemplateRequestDto
{
    public List<int> Days { get; set; } = new();
    public List<int> FoodItemIds { get; set; } = new();
    public string MealType { get; set; } = string.Empty;
    public int DefaultQuantity { get; set; }
}

public class MenuOperationResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Id { get; set; }
    public int ItemsAdded { get; set; }
    public int AvailableQuantity { get; set; }
    public int InitialQuantity { get; set; }
}

public class MenuPreviewItemDto
{
    public int DailyMenuID { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int AvailableQuantity { get; set; }
    public bool IsAvailable { get; set; }
}

public class TemplateExportDto
{
    public int TemplateID { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public int DayOfWeek { get; set; }
    public string FoodItem { get; set; } = string.Empty;
    public string MealType { get; set; } = string.Empty;
    public int DefaultQuantity { get; set; }
    public int DisplayOrder { get; set; }
}
