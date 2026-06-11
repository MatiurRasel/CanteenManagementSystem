// =============================================================================
// PaymentGatewayConfig  (Domain.Payments)  Table: CanteenPaymentGatewayConfigs
// -----------------------------------------------------------------------------
// One row per (tenant, gateway-code). Holds BOTH live and sandbox credentials
// side-by-side; the IsSandbox flag picks which set the runtime reads.
//
// FLOW (admin)
//   1. POST /admin/gateways -> create row with GatewayCode='bkash', sandbox creds.
//   2. POST /admin/gateways/{id}/test-connection -> Status -> Testing.
//   3. Paste live creds, set IsSandbox=false, Status -> Live.
//
// FLOW (runtime)
//   IGatewayConfigService.ResolveAsync(PaymentMethod):
//     -> SELECT row WHERE Method=@m AND IsEnabled=1 AND tenant filter
//     -> project Sandbox* or Live* fields based on IsSandbox
//     -> return GatewayCredentials record
//
// CHANNELS
//   Sub-channels (bKash via SSLCommerz, Visa via SSLCommerz, etc.) live on
//   <see cref="GatewayChannel"/> and link back via GatewayConfigId.
//
// MIRRORS  Fees.Domain.Models.GatewayConfig in CloudCampusPortal/cc24, with
// the platform-master fields folded inline so we only need 2 tables instead
// of 3.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;

namespace Platform.Domain.Payments;

[Table("CanteenPaymentGatewayConfigs")]
public class PaymentGatewayConfig : ITenantOwned
{
    [Key]
    public int GatewayConfigId { get; set; }

    // -------------------- master-style identity (was in CanteenGateway) -----
    /// <summary>Stable code used as a lookup key. e.g. 'bkash', 'nagad', 'ssl', 'stripe'.</summary>
    [Required, StringLength(32)]
    public string GatewayCode { get; set; } = string.Empty;

    /// <summary>Display name e.g. "bKash".</summary>
    [Required, StringLength(64)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Short descriptor for the gateway tile.</summary>
    [StringLength(128)]
    public string? SubTitle { get; set; }

    /// <summary>Concrete enum used by the dispatcher to pick the IPaymentGateway impl.</summary>
    [Required]
    public PaymentMethod Method { get; set; }

    /// <summary>UI group bucket: "mfs" | "cards" | "ibank" | "agg" | "cash".</summary>
    [Required, StringLength(16)]
    public string GatewayGroup { get; set; } = "agg";

    [StringLength(256)] public string? LogoUrl { get; set; }
    public int SortOrder { get; set; }

    // -------------------- environment switch -------------------------------
    public bool IsEnabled { get; set; } = true;
    public bool IsSandbox { get; set; } = true;
    public CanteenGatewayStatus Status { get; set; } = CanteenGatewayStatus.Configured;

    [StringLength(8)] public string Currency { get; set; } = "BDT";

    // -------------------- live credentials ---------------------------------
    [StringLength(512)] public string? LiveBaseUrl { get; set; }
    [StringLength(128)] public string? LiveUsername { get; set; }
    [StringLength(256)] public string? LivePassword { get; set; }
    [StringLength(256)] public string? LiveAppKey { get; set; }
    [StringLength(512)] public string? LiveAppSecret { get; set; }
    [StringLength(128)] public string? LiveMerchantId { get; set; }
    [StringLength(128)] public string? LiveMerchantNumber { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? LivePublicKey { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? LivePrivateKey { get; set; }
    [StringLength(512)] public string? LiveCallbackUrl { get; set; }
    [StringLength(512)] public string? LiveWebhookUrl { get; set; }
    [StringLength(512)] public string? LiveIpnUrl { get; set; }
    [StringLength(512)] public string? LiveFailCallbackUrl { get; set; }

    // -------------------- sandbox credentials ------------------------------
    [StringLength(512)] public string? SandboxBaseUrl { get; set; }
    [StringLength(128)] public string? SandboxUsername { get; set; }
    [StringLength(256)] public string? SandboxPassword { get; set; }
    [StringLength(256)] public string? SandboxAppKey { get; set; }
    [StringLength(512)] public string? SandboxAppSecret { get; set; }
    [StringLength(128)] public string? SandboxMerchantId { get; set; }
    [StringLength(128)] public string? SandboxMerchantNumber { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? SandboxPublicKey { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? SandboxPrivateKey { get; set; }
    [StringLength(512)] public string? SandboxCallbackUrl { get; set; }
    [StringLength(512)] public string? SandboxWebhookUrl { get; set; }
    [StringLength(512)] public string? SandboxIpnUrl { get; set; }
    [StringLength(512)] public string? SandboxFailCallbackUrl { get; set; }

    // -------------------- connectivity test --------------------------------
    public CanteenGatewayStatus? LastTestStatus { get; set; }
    public DateTime? LastTestedAtUtc { get; set; }
    [StringLength(512)] public string? LastTestMessage { get; set; }

    // -------------------- notifications (per-tenant toggles) ---------------
    public bool NotifyOnSuccess { get; set; } = true;
    public bool NotifyOnFailure { get; set; } = true;
    public bool NotifyOnRefund  { get; set; } = true;

    // -------------------- audit + concurrency ------------------------------
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    [StringLength(100)] public string? CreatedBy { get; set; }
    [StringLength(100)] public string? UpdatedBy { get; set; }
    public byte[]? RowVersion { get; set; }

    // -------------------- relations ----------------------------------------
    public ICollection<GatewayChannel> Channels { get; set; } = new List<GatewayChannel>();
}
