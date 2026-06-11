// =============================================================================
// LoyaltyAccount / LoyaltyEntry  (Platform.Domain.Loyalty)
// -----------------------------------------------------------------------------
// One LoyaltyAccount per user; LoyaltyEntry rows are an append-only ledger
// of earn / spend transitions. UI shows live balance by summing the ledger
// (or the materialised PointsBalance, kept in sync as a denormalised cache).
//
// EARN RULES (read from tenant settings at command time — no hard-coded rates):
//   Loyalty.EarnRate               decimal   e.g. "0.10" = 10% back as points
//   Loyalty.MinSpendToEarn         decimal   skip earning on tiny orders
//   Loyalty.PointsPerCurrency      decimal   redemption rate, e.g. "1" → 1 pt = ৳1
//   Loyalty.ExpiryMonths           int       default 12; 0 = never expires
//
// LIFECYCLE
//   OrderDelivered  → POSITIVE entry "Earn"
//   PointsRedeemed  → NEGATIVE entry "Spend"
//   Expiry sweep    → NEGATIVE entry "Expire"  (background job, TBD)
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;

namespace Platform.Domain.Loyalty;

[Table("LoyaltyAccounts")]
public class LoyaltyAccount : ITenantOwned
{
    [Key] public int LoyaltyAccountId { get; set; }

    /// <summary>Source-system external id (matches Student/Employee ExternalId).</summary>
    [Required, StringLength(50)]
    public string UserExternalId { get; set; } = string.Empty;

    [Column(TypeName = "decimal(12,2)")]
    public decimal PointsBalance { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastEarnedAtUtc { get; set; }
    public DateTime? LastRedeemedAtUtc { get; set; }
}

public enum LoyaltyEntryType
{
    Earn   = 1,
    Spend  = 2,
    Expire = 3,
    Adjust = 4
}

[Table("LoyaltyEntries")]
public class LoyaltyEntry : ITenantOwned
{
    [Key] public long LoyaltyEntryId { get; set; }

    public int LoyaltyAccountId { get; set; }

    [ForeignKey(nameof(LoyaltyAccountId))]
    public LoyaltyAccount? Account { get; set; }

    [Required]
    public LoyaltyEntryType EntryType { get; set; }

    /// <summary>Positive for Earn / negative for Spend / Expire.</summary>
    [Column(TypeName = "decimal(12,2)")]
    public decimal Delta { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal BalanceAfter { get; set; }

    public int? OrderId { get; set; }

    [StringLength(200)]
    public string? Reason { get; set; }

    [Required]
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
