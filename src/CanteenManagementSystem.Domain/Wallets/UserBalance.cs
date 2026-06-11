using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Orders;

namespace CanteenManagementSystem.Domain.Wallets;

[Table("CanteenUserBalances")]
public class UserBalance : IAggregateRoot, ITenantOwned
{
    [Key]
    public int BalanceID { get; set; }

    [Required, StringLength(15)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public CanteenUserType UserType { get; set; }

    [Required, Column(TypeName = "decimal(10,2)")]
    public decimal TotalBalance { get; set; }

    [Required, Column(TypeName = "decimal(10,2)")]
    public decimal UsedBalance { get; set; } = 0;

    [Column(TypeName = "decimal(10,2)")]
    public decimal BlockedAmount { get; set; } = 0;

    [Column(TypeName = "decimal(10,2)")]
    public decimal EmergencyEntitlement { get; set; } = 0;

    [Column(TypeName = "decimal(10,2)")]
    public decimal EmergencyUsed { get; set; } = 0;

    [NotMapped]
    public decimal AvailableBalance => TotalBalance - UsedBalance - BlockedAmount;

    [NotMapped]
    public decimal EmergencyAvailable => EmergencyEntitlement - EmergencyUsed;

    public DateTime LastUpdated { get; set; } = DateTime.Now;

    public byte[]? RowVersion { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
