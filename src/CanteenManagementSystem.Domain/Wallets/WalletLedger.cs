using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;

namespace CanteenManagementSystem.Domain.Wallets;

/// Append-only ledger of every wallet movement. Source of truth for financial
/// reconciliation; the UserBalance row is a materialised cache of this ledger.
[Table("CanteenWalletLedger")]
public class WalletLedger : ITenantOwned
{
    [Key]
    public long LedgerID { get; set; }

    public int BalanceID { get; set; }

    [Required, StringLength(15)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public WalletLedgerEntryType EntryType { get; set; }

    [Required, Column(TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal BalanceAfter { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal BlockedAfter { get; set; }

    public int? OrderID { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }

    [StringLength(64)]
    public string? IdempotencyKey { get; set; }

    [Required]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    public string? CreatedBy { get; set; }
}
