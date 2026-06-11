// =============================================================================
// PushSubscription  (Platform.Domain.Notifications)
// -----------------------------------------------------------------------------
// Persisted Web Push subscription per browser/device. The endpoint + p256dh +
// auth tuple is the W3C PushSubscription contract — the server uses it to
// POST encrypted payloads to the user's browser even when the tab is closed.
//
// LIFECYCLE
//   * Browser hits POST /push/subscribe with the W3C subscription JSON.
//   * Service stores one row per (UserId, Endpoint) — unique on that pair.
//   * Sends are best-effort: a 410 from the push service marks the row
//     `IsActive = false` so we stop poking dead subscriptions.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;

namespace Platform.Domain.Notifications;

[Table("CanteenPushSubscription")]
public class PushSubscription : ITenantOwned
{
    [Key] public long PushSubscriptionId { get; set; }

    [Required, StringLength(64)]  public string UserId   { get; set; } = string.Empty;
    [Required, StringLength(500)] public string Endpoint { get; set; } = string.Empty;
    [Required, StringLength(200)] public string P256dh   { get; set; } = string.Empty;
    [Required, StringLength(80)]  public string Auth     { get; set; } = string.Empty;

    [StringLength(200)] public string? UserAgent { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAtUtc { get; set; }
}
