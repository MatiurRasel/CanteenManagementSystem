// =============================================================================
// UserNotificationPreference  (Domain.Notifications)
// -----------------------------------------------------------------------------
// One row per (UserId, TemplateKey, Channel) opt-out. Absence of a row means
// the user has NOT opted out (default: receive notifications). The
// NotificationService consults this table on every send and silently drops
// the message when the matching opt-out is present.
//
// SCHEMA NOTES
//   * `UserId` is the external user identifier (Students.ExternalId /
//     Employees.ExternalId / Users.UserName) — no FK so the row survives
//     deletes/re-issues.
//   * `TemplateKey` matches NotificationTemplates.* keys, OR the literal "*"
//     to opt the user out of an entire channel regardless of template.
//   * `Channel` matches NotificationChannel enum, OR a row with Channel=null
//     opts the user out of EVERY channel for the template.
//   * Both `*` template-key and null Channel resolve to the most-specific match.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;

namespace Platform.Domain.Notifications;

[Table("CanteenUserNotificationPreference")]
public class UserNotificationPreference : ITenantOwned
{
    [Key] public long PreferenceId { get; set; }

    /// <summary>External user identifier (Students.ExternalId / Employees.ExternalId / Users.UserName).</summary>
    [Required, StringLength(64)] public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Template key (e.g. <c>NotificationTemplates.OrderPlaced</c>) or the
    /// literal <c>"*"</c> to opt out of every template on this channel.
    /// </summary>
    [Required, StringLength(60)] public string TemplateKey { get; set; } = "*";

    /// <summary>Channel to opt out of. Null = opt out across all channels for the template.</summary>
    public NotificationChannel? Channel { get; set; }

    /// <summary>Free-text reason captured when the user opts out (e.g. "no SMS at night"). Optional.</summary>
    [StringLength(200)] public string? Reason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
