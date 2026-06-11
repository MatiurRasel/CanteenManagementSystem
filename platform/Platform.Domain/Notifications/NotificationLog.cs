// =============================================================================
// NotificationLog  (Domain.Notifications)
// -----------------------------------------------------------------------------
// One row per outbound message attempt across SMS / Email / WhatsApp / Push.
// Records the rendered body so support can confirm exactly what was sent and
// gives ops a graph of channel health.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;

namespace Platform.Domain.Notifications;

[Table("CanteenNotificationLog")]
public class NotificationLog : ITenantOwned
{
    [Key] public long NotificationId { get; set; }

    [Required, StringLength(50)] public string TemplateKey { get; set; } = string.Empty;
    [Required] public NotificationChannel Channel { get; set; }
    [Required] public NotificationStatus Status { get; set; } = NotificationStatus.Queued;

    [StringLength(200)] public string? Recipient { get; set; }
    [StringLength(200)] public string? Subject { get; set; }
    [StringLength(4000)] public string? Body { get; set; }

    [StringLength(100)] public string? ProviderId { get; set; }
    [StringLength(500)] public string? FailureReason { get; set; }

    public int AttemptCount { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DeliveredAtUtc { get; set; }
}
