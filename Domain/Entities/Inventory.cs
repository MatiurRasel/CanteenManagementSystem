using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CanteenManagementSystem.Domain.Entities
{
    /// <summary>
    /// Inventory management with stock reservation
    /// </summary>
    [Table("Inventory")]
    public class Inventory
    {
        [Key]
        public Guid InventoryId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ClientId { get; set; }

        [Required]
        public Guid ItemId { get; set; }

        // Stock Levels
        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalStock { get; set; } = 0.00m;

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal ReservedStock { get; set; } = 0.00m;

        // Computed: AvailableStock = TotalStock - ReservedStock

        // Alert Levels
        [Column(TypeName = "decimal(10,2)")]
        public decimal MinimumStockAlert { get; set; } = 10.00m;

        [Column(TypeName = "decimal(10,2)")]
        public decimal OptimalStockLevel { get; set; } = 100.00m;

        // Unit
        [StringLength(20)]
        public string UnitOfMeasurement { get; set; } = "PIECE"; // KG, LITRE, PIECE, PLATE, GRAM, ML

        // Valuation
        [Column(TypeName = "decimal(10,2)")]
        public decimal CostPerUnit { get; set; } = 0.00m;

        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalValue { get; set; } = 0.00m; // Computed: TotalStock * CostPerUnit

        [Column(TypeName = "decimal(10,2)")]
        public decimal? LastPurchasePrice { get; set; }

        // Tracking
        public DateTime? LastRestockedAt { get; set; }
        public Guid? LastRestockedBy { get; set; }
        public DateTime? LastUpdatedAt { get; set; }

        // Alerts
        public bool AlertsEnabled { get; set; } = true;
        public DateTime? LowStockNotifiedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("ClientId")]
        public virtual Client Client { get; set; } = null!;
        [ForeignKey("ItemId")]
        public virtual MenuItem MenuItem { get; set; } = null!;
        public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
    }
}

