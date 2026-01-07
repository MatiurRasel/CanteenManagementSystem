using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CanteenManagementSystem.Domain.Entities
{
    /// <summary>
    /// Menu category (Breakfast, Lunch, Dinner, Snacks, Beverages)
    /// </summary>
    [Table("MenuCategories")]
    public class MenuCategory
    {
        [Key]
        public Guid CategoryId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ClientId { get; set; }

        [Required]
        [StringLength(100)]
        public string CategoryName { get; set; } = string.Empty;

        public string? CategoryDescription { get; set; }

        [StringLength(20)]
        public string? CategoryType { get; set; } // BREAKFAST, LUNCH, DINNER, SNACKS, BEVERAGES, DESSERTS

        // Display
        public string? CategoryIconUrl { get; set; }
        public int DisplayOrder { get; set; } = 0;

        // Status
        public bool IsActive { get; set; } = true;

        // Audit
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("ClientId")]
        public virtual Client Client { get; set; } = null!;
        public virtual ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
    }
}

