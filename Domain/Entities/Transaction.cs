using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CanteenManagementSystem.Domain.Entities
{
    /// <summary>
    /// Transaction ledger for wallet operations
    /// </summary>
    [Table("Transactions")]
    public class Transaction
    {
        [Key]
        public Guid TransactionId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid UserId { get; set; }

        [Required]
        public Guid ClientId { get; set; }

        [Required]
        public Guid WalletId { get; set; }

        // Transaction Details
        [Required]
        [StringLength(20)]
        public string TransactionType { get; set; } = "ORDER"; // RECHARGE, ORDER, REFUND, DEDUCTION, ADJUSTMENT, EMERGENCY_RECOVERY

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal BalanceBefore { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal BalanceAfter { get; set; }

        // Emergency Balance
        [Column(TypeName = "decimal(10,2)")]
        public decimal EmergencyBalanceRecovered { get; set; } = 0.00m;

        [Column(TypeName = "decimal(10,2)")]
        public decimal EmergencyBalanceUsedInTxn { get; set; } = 0.00m;

        // Payment Details
        [StringLength(20)]
        public string? PaymentMethod { get; set; } // ONLINE, CASH, CHEQUE, UPI, CARD, NET_BANKING, ADJUSTMENT

        [StringLength(255)]
        public string? PaymentReference { get; set; }

        [StringLength(255)]
        public string? PaymentGatewayTxnId { get; set; }

        // Related Entities
        public Guid? OrderId { get; set; }

        // Description
        [Required]
        public string Description { get; set; } = string.Empty;
        public string? Notes { get; set; }

        // Metadata (JSON)
        public string? MetadataJson { get; set; }

        // Audit
        public Guid? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("WalletId")]
        public virtual UserBalance UserBalance { get; set; } = null!;
        [ForeignKey("OrderId")]
        public virtual Order? Order { get; set; }
    }
}

