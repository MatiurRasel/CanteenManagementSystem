// =============================================================================
// WebhookSubscription / WebhookDelivery  (Platform.Domain.Webhooks)
// -----------------------------------------------------------------------------
// Outbound webhook subscriptions. Each WebhookSubscription points at a partner
// URL + carries a secret used to sign deliveries (same HMAC scheme the inbound
// API key flow uses, just inverted). Events the partner subscribes to are
// stored as a CSV — small set, no JOIN table needed.
//
// EVENT KEYS (canonical)
//   order.placed         — fired by PlaceOrderCommandHandler
//   order.delivered      — fired by MarkOrderDeliveredCommandHandler
//   order.voided         — fired by VoidOrderCommandHandler
//   order.refunded       — fired by VoidOrderCommandHandler (post-delivery path)
//   wallet.recharged     — fired by WalletController.Recharge
//   card.lost            — fired by CardsAdminController.ReportLost
//   directory.synced     — fired by DirectorySyncService on success
//
// DELIVERY MODEL
//   * Dispatcher writes a WebhookDelivery row for every fire (Status="Queued").
//   * Background worker picks Queued rows, POSTs to URL with HMAC headers.
//   * On 2xx → "Delivered" + ResponseCode; 4xx/5xx → "Failed" + NextRetryAtUtc
//     (exponential backoff, capped at 24 h after 8 failures).
//   * Partner can replay any failed delivery from the admin UI.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;

namespace Platform.Domain.Webhooks;

[Table("WebhookSubscriptions")]
public class WebhookSubscription : ITenantOwned
{
    [Key] public int SubscriptionId { get; set; }

    [Required, StringLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, StringLength(512)]
    public string Url { get; set; } = string.Empty;

    /// <summary>HMAC signing secret. Persisted with IsSecret=… semantics by the admin form (encrypted at rest).</summary>
    [Required, StringLength(256)]
    public string Secret { get; set; } = string.Empty;

    /// <summary>CSV of event keys the partner subscribes to.</summary>
    [Required, StringLength(500)]
    public string EventsCsv { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int FailureCount { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastSuccessAtUtc { get; set; }
    public DateTime? LastFailureAtUtc { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }
}

[Table("WebhookDeliveries")]
public class WebhookDelivery : ITenantOwned
{
    [Key] public long DeliveryId { get; set; }

    public int SubscriptionId { get; set; }

    [ForeignKey(nameof(SubscriptionId))]
    public WebhookSubscription? Subscription { get; set; }

    [Required, StringLength(64)]
    public string EventKey { get; set; } = string.Empty;

    /// <summary>JSON payload as queued. Frozen — replays send the same bytes.</summary>
    [Required, Column(TypeName = "nvarchar(max)")]
    public string PayloadJson { get; set; } = "{}";

    /// <summary>"Queued" | "Delivering" | "Delivered" | "Failed".</summary>
    [Required, StringLength(16)]
    public string Status { get; set; } = "Queued";

    public int Attempt { get; set; }
    public int ResponseCode { get; set; }

    [StringLength(2000)]
    public string? ResponseSnippet { get; set; }

    public DateTime? FirstQueuedAtUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? NextRetryAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
