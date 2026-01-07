namespace CanteenManagementSystem.Application.DTOs
{
    public class MenuItemDto
    {
        public Guid ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string ItemType { get; set; } = string.Empty;
        public bool IsVeg { get; set; }
        public decimal BasePrice { get; set; }
        public decimal UserPrice { get; set; } // Role-based price
        public decimal TaxPercentage { get; set; }
        public decimal FinalPrice { get; set; }
        public bool IsAvailable { get; set; }
        public int? RemainingQuantity { get; set; }
        public int PreparationTime { get; set; }
        public List<string> Images { get; set; } = new();
        public DietaryInfoDto? DietaryInfo { get; set; }
    }

    public class DietaryInfoDto
    {
        public List<string> Allergens { get; set; } = new();
        public string? SpiceLevel { get; set; }
        public int? Calories { get; set; }
    }

    public class MenuCategoryDto
    {
        public Guid CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? CategoryType { get; set; }
        public List<MenuItemDto> Items { get; set; } = new();
    }
}

