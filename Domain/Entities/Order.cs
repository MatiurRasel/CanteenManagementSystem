using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CanteenManagementSystem.Domain.Entities
{
    /// <summary>
    /// Order entity with offline support
    /// </summary>
    [Table("Orders")]
    public class Order
    {
        [Key]
        public Guid OrderId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ClientId { get; set; }

        public Guid? UserId { get; set; } // Nullable for anonymous orders

        // Order Identification
        [Required]
        [StringLength(50)]
        public string OrderNumber { get; set; } = string.Empty; // ORD-2024-12345

        [Required]
        [StringLength(20)]
        public string TokenNumber { get; set; } = string.Empty; // T-42

        // Order Type
        [Required]
        [StringLength(20)]
        public string OrderType { get; set; } = "INSTANT"; // PREORDER, INSTANT, ANONYMOUS

        // Status
        [Required]
        [StringLength(20)]
        public string OrderStatus { get; set; } = "PLACED"; // PLACED, CONFIRMED, PREPARING, READY, DELIVERED, CANCELLED

        [Required]
        [StringLength(20)]
        public string PaymentStatus { get; set; } = "PENDING"; // PENDING, BLOCKED, COMPLETED, REFUNDED, FAILED

        // Amounts
        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal TaxAmount { get; set; } = 0.00m;

        [Column(TypeName = "decimal(10,2)")]
        public decimal DiscountAmount { get; set; } = 0.00m;

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalAmount { get; set; }

        // Payment Details
        public bool AmountBlocked { get; set; } = false;
        [Column(TypeName = "decimal(10,2)")]
        public decimal DeductedFromMain { get; set; } = 0.00m;
        [Column(TypeName = "decimal(10,2)")]
        public decimal DeductedFromEmergency { get; set; } = 0.00m;
        [StringLength(20)]
        public string? PaymentMethod { get; set; }

        // Delivery Information
        public DateTime? DeliveryTimeSlot { get; set; }
        [StringLength(20)]
        public string? TableNumber { get; set; }
        [StringLength(50)]
        public string? RoomNumber { get; set; }
        public string? SpecialInstructions { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? PreparingAt { get; set; }
        public DateTime? ReadyAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public DateTime? AutoCancelAt { get; set; }

        // Cancellation
        public Guid? CancelledBy { get; set; }
        public string? CancellationReason { get; set; }
        public bool IsAutoCancelled { get; set; } = false;

        // NFC & Operator
        [StringLength(100)]
        public string? NfcCardUsed { get; set; }
        public Guid? PreparedBy { get; set; }
        public Guid? DeliveredBy { get; set; }

        // Rating (optional)
        public int? Rating { get; set; } // 1-5
        public string? Feedback { get; set; }

        // Metadata (JSON)
        public string? MetadataJson { get; set; }

        // Offline Sync Support
        public Guid? LocalOrderId { get; set; } // For offline orders
        [StringLength(20)]
        public string SyncStatus { get; set; } = "SYNCED"; // PENDING, SYNCING, SYNCED, FAILED
        public int SyncAttempts { get; set; } = 0;
        public DateTime? LastSyncAttemptAt { get; set; }
        public DateTime? SyncedAt { get; set; }
        public string? SyncError { get; set; }

        // Audit
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("ClientId")]
        public virtual Client Client { get; set; } = null!;
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }
        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}

