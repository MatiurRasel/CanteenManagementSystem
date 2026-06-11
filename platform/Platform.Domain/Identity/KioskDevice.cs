// =============================================================================
// KioskDevice  (Platform.Domain.Identity)
// -----------------------------------------------------------------------------
// A paired self-service kiosk tablet. An admin issues a device row + a one-time
// pairing token; the device redeems the token at /kiosk/pair?token=… and from
// then on holds a long-lived cookie that authenticates it as a Kiosk principal.
//
// SECURITY MODEL
//   * The raw token is shown ONCE at issuance (and rendered as a QR for easy
//     scanning by the tablet's camera) — we store ONLY the PBKDF2 hash.
//   * Each device is tenant-scoped (ITenantOwned) so revoking a tenant
//     cascades to its kiosks.
//   * Revoking a device flips IsActive=false; future cookie validation
//     checks this and signs the device out.
//   * LastSeenAtUtc lets admins spot tablets that are offline / stolen.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;

namespace Platform.Domain.Identity;

[Table("AppKioskDevices")]
public class KioskDevice : ITenantOwned
{
    [Key]
    public int KioskDeviceId { get; set; }

    /// <summary>Human-friendly label set by admin ("Counter A tablet", "Lobby kiosk").</summary>
    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Hex PBKDF2 hash of the raw pairing token.</summary>
    [Required, StringLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Hex salt used to derive TokenHash.</summary>
    [Required, StringLength(64)]
    public string Salt { get; set; } = string.Empty;

    /// <summary>First 4 chars of the raw token — UI hint so admins can identify it later.</summary>
    [Required, StringLength(8)]
    public string TokenPrefix { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RedeemedAtUtc { get; set; }
    public DateTime? LastSeenAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>UserId of the admin who issued this device. Audit only.</summary>
    public int? IssuedByUserId { get; set; }

    [StringLength(45)]
    public string? LastSeenIp { get; set; }
}
