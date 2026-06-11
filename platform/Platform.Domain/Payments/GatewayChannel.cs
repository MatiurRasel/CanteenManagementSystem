// =============================================================================
// GatewayChannel  (Domain.Payments)  Table: CanteenGatewayChannels
// -----------------------------------------------------------------------------
// A payment channel offered under a gateway. For aggregators like SSLCommerz
// you'll have multiple rows (bKash via SSL, Visa via SSL, Nagad via SSL) each
// with its own charge configuration. For direct integrations you typically
// have one channel.
//
// CHARGE CALCULATION
//   Mirrors CC24's GatewayChannel.ComputeCharges:
//     bankCharge   = ChargeType=='P' ? gross*TotalCharge/100 : TotalCharge
//                    clamped to [MinChargeAmount, MaxChargeAmount] (0 = unbounded)
//     ourMarkup    = ChargeType=='P' ? gross*OurMarkup/100 : OurMarkup
//     vatTax       = (bankCharge + ourMarkup) * VatTaxPercent / 100
//     netSettled   = gross - bankCharge - ourMarkup - vatTax
//
// USAGE
//   - On checkout, the user picks a channel (or the orchestrator auto-resolves
//     when only one is active for the chosen gateway).
//   - PaymentTransaction.ChannelCode records which channel was used so reports
//     can break down volume per channel.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;

namespace Platform.Domain.Payments;

[Table("CanteenGatewayChannels")]
public class GatewayChannel : ITenantOwned
{
    [Key]
    public int ChannelId { get; set; }

    /// <summary>FK to <see cref="PaymentGatewayConfig"/>.</summary>
    public int GatewayConfigId { get; set; }

    [ForeignKey(nameof(GatewayConfigId))]
    public PaymentGatewayConfig? GatewayConfig { get; set; }

    /// <summary>Stable channel identifier persisted on transactions ('bkash', 'visa', 'nagad', 'rocket', 'direct').</summary>
    [Required, StringLength(64)]
    public string ChannelCode { get; set; } = string.Empty;

    [Required, StringLength(128)]
    public string ChannelName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    // -------------------- charge configuration -----------------------------
    /// <summary>'P' = percent, 'F' = flat fee.</summary>
    [Required, StringLength(1)]
    public string ChargeType { get; set; } = "P";

    [Column(TypeName = "decimal(10,4)")] public decimal TotalCharge { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal MinChargeAmount { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal MaxChargeAmount { get; set; }
    [Column(TypeName = "decimal(10,4)")] public decimal VatTaxPercent { get; set; }
    [Column(TypeName = "decimal(10,4)")] public decimal OurMarkup { get; set; }

    [StringLength(256)] public string? LogoUrl { get; set; }

    // -------------------- audit --------------------------------------------
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Compute the charge breakdown for a gross transaction amount.</summary>
    public (decimal BankCharge, decimal OurCommission, decimal VatTax, decimal NetSettled) ComputeCharges(decimal grossAmount)
    {
        var isPercent = string.Equals(ChargeType?.Trim(), "P", StringComparison.OrdinalIgnoreCase);

        var bankCharge = isPercent ? grossAmount * TotalCharge / 100m : TotalCharge;
        if (MinChargeAmount > 0 && bankCharge < MinChargeAmount) bankCharge = MinChargeAmount;
        if (MaxChargeAmount > 0 && bankCharge > MaxChargeAmount) bankCharge = MaxChargeAmount;

        var ourCommission = isPercent ? grossAmount * OurMarkup / 100m : OurMarkup;
        var vatTax        = (bankCharge + ourCommission) * VatTaxPercent / 100m;
        var netSettled    = grossAmount - bankCharge - ourCommission - vatTax;

        return (Math.Round(bankCharge,    2),
                Math.Round(ourCommission, 2),
                Math.Round(vatTax,        2),
                Math.Round(netSettled,    2));
    }
}
