using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CanteenManagementSystem.Domain.Entities
{
    /// <summary>
    /// Inventory transaction log
    /// </summary>
    [Table("InventoryTransactions")]
    public class InventoryTransaction
    {
        [Key]
        public Guid TransactionId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid InventoryId { get; set; }

        [Required]
        public Guid ClientId { get; set; }

        // Transaction Details
        [Required]
        [StringLength(20)]
        public string TransactionType { get; set; } = "SALE"; // RESTOCK, SALE, WASTAGE, ADJUSTMENT, RETURN, RESERVE, RELEASE

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Quantity { get; set; }

        // Before/After
        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal StockBefore { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal StockAfter { get; set; }

        // Related Entities
        public Guid? RelatedOrderId { get; set; }
        public Guid? RelatedPurchaseId { get; set; }

        // Cost
        [Column(TypeName = "decimal(10,2)")]
        public decimal? CostPerUnit { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? TotalCost { get; set; }

        // Details
        public string? Notes { get; set; }

        // Audit
        public Guid? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("InventoryId")]
        public virtual Inventory Inventory { get; set; } = null!;
    }
}

