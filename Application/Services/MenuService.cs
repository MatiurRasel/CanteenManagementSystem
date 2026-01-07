using CanteenManagementSystem.Application.DTOs;
using CanteenManagementSystem.Application.Interfaces;
using CanteenManagementSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CanteenManagementSystem.Application.Services
{
    public class MenuService : IMenuService
    {
        private readonly ApplicationDbContext _context;

        public MenuService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<MenuCategoryDto>> GetMenuAsync(Guid clientId, string? userRole = null)
        {
            var categories = await _context.MenuCategories
                .Where(c => c.ClientId == clientId && c.IsActive)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            var menuCategories = new List<MenuCategoryDto>();

            foreach (var category in categories)
            {
                var items = await _context.MenuItems
                    .Include(m => m.Inventory)
                    .Where(m => m.CategoryId == category.CategoryId && m.IsActive && m.IsAvailable)
                    .OrderBy(m => m.DisplayOrder)
                    .ToListAsync();

                var itemDtos = items.Select(item => MapToDto(item, userRole)).ToList();

                menuCategories.Add(new MenuCategoryDto
                {
                    CategoryId = category.CategoryId,
                    CategoryName = category.CategoryName,
                    CategoryType = category.CategoryType,
                    Items = itemDtos
                });
            }

            return menuCategories;
        }

        public async Task<MenuItemDto> GetMenuItemAsync(Guid itemId)
        {
            var item = await _context.MenuItems
                .Include(m => m.Inventory)
                .FirstOrDefaultAsync(m => m.ItemId == itemId);

            if (item == null)
                throw new InvalidOperationException("Menu item not found");

            return MapToDto(item, null);
        }

        public async Task<bool> UpdateItemAvailabilityAsync(Guid itemId, bool isAvailable)
        {
            var item = await _context.MenuItems.FindAsync(itemId);
            if (item == null)
                return false;

            item.IsAvailable = isAvailable;
            item.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<MenuItemDto>> GetFeaturedItemsAsync(Guid clientId)
        {
            var items = await _context.MenuItems
                .Include(m => m.Inventory)
                .Where(m => m.ClientId == clientId && m.IsActive && m.IsAvailable && m.IsFeatured)
                .OrderBy(m => m.DisplayOrder)
                .ToListAsync();

            return items.Select(item => MapToDto(item, null)).ToList();
        }

        private MenuItemDto MapToDto(Domain.Entities.MenuItem item, string? userRole)
        {
            // Get role-based price
            var userPrice = GetRoleBasedPrice(item, userRole);
            var taxAmount = userPrice * (item.TaxPercentage / 100);
            var finalPrice = userPrice + taxAmount;

            // Parse images
            var images = new List<string>();
            if (!string.IsNullOrEmpty(item.ImagesJson) && item.ImagesJson != "[]")
            {
                try
                {
                    images = System.Text.Json.JsonSerializer.Deserialize<List<string>>(item.ImagesJson) ?? new();
                }
                catch { }
            }

            // Parse dietary info
            var allergens = new List<string>();
            if (!string.IsNullOrEmpty(item.AllergensJson) && item.AllergensJson != "[]")
            {
                try
                {
                    allergens = System.Text.Json.JsonSerializer.Deserialize<List<string>>(item.AllergensJson) ?? new();
                }
                catch { }
            }

            return new MenuItemDto
            {
                ItemId = item.ItemId,
                ItemName = item.ItemName,
                Description = item.ItemDescription,
                CategoryName = string.Empty, // Will be set by caller
                ItemType = item.ItemType,
                IsVeg = item.IsVeg,
                BasePrice = item.BasePrice,
                UserPrice = userPrice,
                TaxPercentage = item.TaxPercentage,
                FinalPrice = finalPrice,
                IsAvailable = item.IsAvailable,
                RemainingQuantity = item.Inventory != null 
                    ? (int?)(item.Inventory.TotalStock - item.Inventory.ReservedStock)
                    : item.RemainingQuantity,
                PreparationTime = item.PreparationTime,
                Images = images,
                DietaryInfo = new DietaryInfoDto
                {
                    Allergens = allergens,
                    SpiceLevel = item.SpiceLevel,
                    Calories = item.Calories
                }
            };
        }

        private decimal GetRoleBasedPrice(Domain.Entities.MenuItem item, string? role)
        {
            if (string.IsNullOrEmpty(role) || string.IsNullOrEmpty(item.RoleBasedPricingJson) || item.RoleBasedPricingJson == "{}")
                return item.BasePrice;

            try
            {
                var pricing = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal>>(item.RoleBasedPricingJson);
                if (pricing != null && pricing.ContainsKey(role))
                    return pricing[role];
            }
            catch { }

            return item.BasePrice;
        }
    }
}

