using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CanteenManagementSystem.Domain.Entities
{
    /// <summary>
    /// Menu item variants (Size, Addons, Customizations)
    /// </summary>
    [Table("MenuItemVariants")]
    public class MenuItemVariant
    {
        [Key]
        public Guid VariantId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ItemId { get; set; }

        // Variant Details
        [Required]
        [StringLength(100)]
        public string VariantName { get; set; } = string.Empty; // e.g., "Size", "Extra Toppings"

        [StringLength(20)]
        public string? VariantType { get; set; } // SIZE, ADDON, CUSTOMIZATION

        [Required]
        public string VariantOptionsJson { get; set; } = "[]"; // [{"name": "Small", "price": 0}, {"name": "Large", "price": 20}]

        public bool IsRequired { get; set; } = false;
        public int DisplayOrder { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("ItemId")]
        public virtual MenuItem MenuItem { get; set; } = null!;
    }
}

