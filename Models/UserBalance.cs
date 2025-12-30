using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CanteenManagementSystem.Models
{
    [Table("CanteenUserBalances")]
    public class UserBalance
    {
        [Key]
        public int BalanceID { get; set; }
        [Required]
        [StringLength(15)]
        public string UserId { get; set; }

        [Required]
        public CanteenUserType UserType { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalBalance { get; set; }

        [Required]
        [Column(TypeName = "decimal(10,2)")]
        public decimal UsedBalance { get; set; } = 0;

        [Column(TypeName = "decimal(10,2)")]
        public decimal AvailableBalance => TotalBalance - UsedBalance;

        public DateTime LastUpdated { get; set; } = DateTime.Now;

        // Navigation property to Orders
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}
