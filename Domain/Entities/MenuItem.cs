using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CanteenManagementSystem.Domain.Entities
{
    /// <summary>
    /// Menu item with pricing, dietary info, and availability
    /// </summary>
    [Table("MenuItems")]
    public class MenuItem
    {
        [Key]
        public Guid ItemId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ClientId { get; set; }

        [Required]
        public Guid CategoryId { get; set; }

        // Basic Info
        [Required]
        [StringLength(255)]
        public string ItemName { get; set; } = string.Empty;

        public string? ItemDescription { get; set; }

        [StringLength(50)]
        public string? ItemCode { get; set; }

        // Type
        [Required]
        [StringLength(20)]
        public string ItemType { get; set; } = "PRECOOKED"; // PRECOOKED, MADE_TO_ORDER, COMBO

        public int PreparationTime { get; set; } = 0; // in minutes

        // Dietary Information
        public bool IsVeg { get; set; } = true;
        public bool IsNonVeg { get; set; } = false;
        public bool IsVegan { get; set; } = false;
        public bool IsJain { get; set; } = false;
        public bool IsGlutenFree { get; set; } = false;
        public string AllergensJson { get; set; } = "[]"; // JSON array
        [StringLength(10)]
        public string? SpiceLevel { get; set; } // NONE, MILD, MEDIUM, HOT, EXTRA_HOT

        // Nutritional Info
        public int? Calories { get; set; }
        [Column(TypeName = "decimal(5,2)")]
        public decimal? ProteinGrams { get; set; }
        [Column(TypeName = "decimal(5,2)")]
        public decimal? CarbsGrams { get; set; }
        [Column(TypeName = "decimal(5,2)")]
        public decimal? FatGrams { get; set; }

        // Pricing
        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal BasePrice { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal TaxPercentage { get; set; } = 5.00m;

        [Column(TypeName = "decimal(10,2)")]
        public decimal? DiscountedPrice { get; set; }

        // Role-based Pricing (JSON)
        public string RoleBasedPricingJson { get; set; } = "{}"; // {"STUDENT": 80.00, "TEACHER": 90.00}

        // Availability
        public bool IsAvailable { get; set; } = true;
        public string AvailableDaysJson { get; set; } = "[\"MON\",\"TUE\",\"WED\",\"THU\",\"FRI\",\"SAT\",\"SUN\"]";
        public string AvailableTimeSlotsJson { get; set; } = "[]"; // JSON array of time ranges

        public int? DailyQuantityLimit { get; set; }
        public int? RemainingQuantity { get; set; }

        // Media
        public string ImagesJson { get; set; } = "[]"; // JSON array of URLs
        public string? PrimaryImageUrl { get; set; }

        // Display
        public int DisplayOrder { get; set; } = 0;
        public bool IsFeatured { get; set; } = false;
        public bool IsBestseller { get; set; } = false;
        public bool IsNew { get; set; } = false;

        // Status
        public bool IsActive { get; set; } = true;
        public string? OutOfStockReason { get; set; }

        // Audit
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("ClientId")]
        public virtual Client Client { get; set; } = null!;
        [ForeignKey("CategoryId")]
        public virtual MenuCategory Category { get; set; } = null!;
        public virtual ICollection<MenuItemVariant> Variants { get; set; } = new List<MenuItemVariant>();
        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public virtual Inventory? Inventory { get; set; }
    }
}

