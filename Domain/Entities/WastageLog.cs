using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CanteenManagementSystem.Domain.Entities
{
    /// <summary>
    /// Food wastage tracking
    /// </summary>
    [Table("WastageLog")]
    public class WastageLog
    {
        [Key]
        public Guid WastageId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ClientId { get; set; }

        [Required]
        public Guid ItemId { get; set; }

        // Wastage Details
        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal QuantityWasted { get; set; }

        [StringLength(20)]
        public string? UnitOfMeasurement { get; set; }

        // Reason
        [StringLength(50)]
        public string? Reason { get; set; } // EXPIRED, DAMAGED, CANCELLED_ORDER, OVERCOOKED, QUALITY_ISSUE, OTHER

        public string? DetailedReason { get; set; }

        // Cost Impact
        [Column(TypeName = "decimal(10,2)")]
        public decimal? CostPerUnit { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? TotalLoss { get; set; }

        // Related
        public Guid? RelatedOrderId { get; set; }

        // Audit
        public Guid? LoggedBy { get; set; }
        public DateTime LoggedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("ItemId")]
        public virtual MenuItem MenuItem { get; set; } = null!;
    }
}

