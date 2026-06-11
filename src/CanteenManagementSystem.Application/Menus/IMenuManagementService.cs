using CanteenManagementSystem.Application.Menus.Dtos;
using CanteenManagementSystem.Application.Menus.ViewModels;
using CanteenManagementSystem.Domain.Menu;

namespace CanteenManagementSystem.Application.Menus;

public interface IMenuManagementService
{
    Task<MenuManagementViewModel> GetManageViewModelAsync(DateTime? date);
    Task<List<FoodItem>> GetManageItemsAsync();
    Task<MenuOperationResultDto> CreateMenuItemAsync(AddMenuItemRequestDto request);
    Task<MenuOperationResultDto> RemoveMenuItemAsync(int menuId);
    Task<MenuOperationResultDto> UpdateQuantityAsync(UpdateQuantityRequestDto request);
    Task<MenuOperationResultDto> ToggleAvailabilityAsync(int menuId);
    Task<MenuOperationResultDto> RemoveFromMenuAsync(int menuId);
    Task<MenuOperationResultDto> CreateItemAsync(CreateFoodItemRequestDto request);
    Task<MenuOperationResultDto> UpdateItemAsync(UpdateFoodItemRequestDto request);
    Task<MenuOperationResultDto> ToggleFoodItemStatusAsync(int id);
    Task<List<MenuPreviewItemDto>> GetMenuPreviewAsync(DateTime date);
    Task<MenuOperationResultDto> QuickApplyTemplateAsync(QuickApplyTemplateRequestDto request);
    Task<MenuOperationResultDto> ApplyDayTemplateAsync(int dayOfWeek);
    Task<MenuOperationResultDto> BatchUpdateQuantitiesAsync(BatchUpdateRequestDto request);
    Task<MenuOperationResultDto> BatchToggleAvailabilityAsync(BatchToggleRequestDto request);
    Task<MenuOperationResultDto> ClearTodayMenuAsync();
    Task<MenuOperationResultDto> CreateBulkTemplateAsync(BulkTemplateRequestDto request);
    Task<List<TemplateExportDto>> ExportTemplatesAsync();
    Task<string> GenerateWeeklyReportAsync();
    Task<MenuOperationResultDto> CreateWeeklyTemplateAsync(CreateTemplateRequestDto request);
    Task<MenuOperationResultDto> DeleteWeeklyTemplateAsync(int templateId);
    Task<MenuOperationResultDto> ApplyWeeklyTemplateAsync(ApplyWeeklyTemplateRequestDto request);
    Task<MenuOperationResultDto> CopyMenuAsync(CopyMenuRequestDto request);

    /// <summary>Read-only lookups used by the menu management UI (per ADR 0004 — controller never queries DbContext).</summary>
    Task<IReadOnlyList<WeeklyTemplateItemDto>> GetWeeklyTemplateAsync(int dayOfWeek, CancellationToken ct = default);
    Task<FoodItemDto?> GetFoodItemAsync(int id, CancellationToken ct = default);
}

public sealed record WeeklyTemplateItemDto(
    int TemplateID, int FoodItemID, string ItemName, decimal Price,
    string MealType, int DefaultQuantity, int DisplayOrder);

public sealed record FoodItemDto(
    int FoodItemID, string ItemName, string? Description, decimal Price,
    string Category, string? ImageUrl, bool IsAvailable);
