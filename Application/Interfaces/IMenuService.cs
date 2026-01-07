using CanteenManagementSystem.Application.DTOs;

namespace CanteenManagementSystem.Application.Interfaces
{
    public interface IMenuService
    {
        Task<List<MenuCategoryDto>> GetMenuAsync(Guid clientId, string? userRole = null);
        Task<MenuItemDto> GetMenuItemAsync(Guid itemId);
        Task<bool> UpdateItemAvailabilityAsync(Guid itemId, bool isAvailable);
        Task<List<MenuItemDto>> GetFeaturedItemsAsync(Guid clientId);
    }
}

