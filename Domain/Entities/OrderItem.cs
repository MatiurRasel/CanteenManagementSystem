using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CanteenManagementSystem.Domain.Entities
{
    /// <summary>
    /// Order item with snapshot data
    /// </summary>
    [Table("OrderItems")]
    public class OrderItem
    {
        [Key]
        public Guid OrderItemId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid OrderId { get; set; }

        [Required]
        public Guid ItemId { get; set; }

        // Item Details (snapshot at order time)
        [Required]
        [StringLength(255)]
        public string ItemName { get; set; } = string.Empty;

        [StringLength(50)]
        public string? ItemCode { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal UnitPrice { get; set; }

        // Customizations (JSON)
        public string CustomizationsJson { get; set; } = "[]"; // [{"variant_name": "Size", "option": "Large", "price": 20}]

        // Amounts
        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal TaxAmount { get; set; } = 0.00m;

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Total { get; set; }

        // Stock Tracking
        public bool StockReserved { get; set; } = false;
        public bool StockDeducted { get; set; } = false;

        // Status
        [StringLength(20)]
        public string ItemStatus { get; set; } = "PENDING"; // PENDING, PREPARING, READY, DELIVERED, CANCELLED

        // Special Instructions
        public string? SpecialInstructions { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("OrderId")]
        public virtual Order Order { get; set; } = null!;
        [ForeignKey("ItemId")]
        public virtual MenuItem MenuItem { get; set; } = null!;
    }
}

