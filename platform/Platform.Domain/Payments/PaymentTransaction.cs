// =============================================================================
// PaymentTransaction  (Domain.Payments)
// -----------------------------------------------------------------------------
// One row per attempt to charge a customer through any gateway. Even a failed
// attempt is persisted so reconciliation reports can compare succeeded vs
// failed and tenants can chase abandoned carts.
//
// FIELDS
//   TransactionRef    Internal reference, generated client-side as a GUID and
//                     used as IdempotencyKey for downstream wallet recharge.
//   GatewayPaymentId  The id the gateway returns (bkash paymentID, ssl tran_id).
//   CallbackPayload   Raw JSON callback body for forensics.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;

namespace Platform.Domain.Payments;

[Table("CanteenPaymentTransactions")]
public class PaymentTransaction : IAggregateRoot, ITenantOwned
{
    [Key] public long PaymentId { get; set; }

    [Required, StringLength(64)]
    public string TransactionRef { get; set; } = Guid.NewGuid().ToString("N");

    [Required, StringLength(15)]
    public string UserId { get; set; } = string.Empty;

    [Required] public string UserType { get; set; } = string.Empty;

    [Required, Column(TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }

    [StringLength(8)] public string Currency { get; set; } = "BDT";
    [Required] public PaymentMethod Method { get; set; }

    /// <summary>
    /// Which sub-channel under the gateway was used (e.g. 'bkash', 'visa', 'nagad').
    /// Matches a row in CanteenGatewayChannels for the same tenant; used by reports
    /// to break down volume per channel. Null for direct single-channel gateways
    /// (Stripe, native bKash) where the channel is implicit.
    /// </summary>
    [StringLength(64)] public string? ChannelCode { get; set; }

    /// <summary>FK to <c>CanteenPaymentGatewayConfigs.GatewayConfigId</c> that was used.</summary>
    public int? GatewayConfigId { get; set; }

    [Required] public PaymentStatus Status { get; set; } = PaymentStatus.Initiated;

    [StringLength(128)] public string? GatewayPaymentId { get; set; }
    [StringLength(256)] public string? GatewayMessage { get; set; }
    [StringLength(4000)] public string? CallbackPayload { get; set; }

    [StringLength(500)] public string? RedirectUrl { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }

    [StringLength(100)] public string? InitiatedBy { get; set; }

    public byte[]? RowVersion { get; set; }
}
