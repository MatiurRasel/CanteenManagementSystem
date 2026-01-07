using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CanteenManagementSystem.Domain.Entities
{
    /// <summary>
    /// User wallet with emergency balance support
    /// </summary>
    [Table("UsersWallet")]
    public class UserBalance
    {
        [Key]
        public Guid WalletId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid UserId { get; set; }

        [Required]
        public Guid ClientId { get; set; }

        // Balances
        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal MainBalance { get; set; } = 0.00m;

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal EmergencyBalanceLimit { get; set; } = 0.00m;

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal EmergencyBalanceUsed { get; set; } = 0.00m;

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal BlockedAmount { get; set; } = 0.00m;

        // Computed properties (calculated in application layer)
        // AvailableBalance = MainBalance + (EmergencyBalanceLimit - EmergencyBalanceUsed)
        // SpendableBalance = AvailableBalance - BlockedAmount

        // Lifetime Tracking
        [Column(TypeName = "decimal(10,2)")]
        public decimal LifetimeRecharge { get; set; } = 0.00m;

        [Column(TypeName = "decimal(10,2)")]
        public decimal LifetimeSpent { get; set; } = 0.00m;

        [Column(TypeName = "decimal(10,2)")]
        public decimal LifetimeRefunded { get; set; } = 0.00m;

        // Tracking
        public DateTime? LastRechargeAt { get; set; }
        public DateTime? LastTransactionAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
        public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}

