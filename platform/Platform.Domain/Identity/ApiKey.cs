// =============================================================================
// ApiKey  (Platform.Domain.Identity)
// -----------------------------------------------------------------------------
// Service-to-service credential. Issued to integrating systems (the school
// portal CCPC, partner ERPs, mobile-app backends). Two-piece credential:
//
//   X-App-Key     32-byte base64url, lookup index (stored in clear).
//   X-App-Secret  32-byte base64url, sent by caller, HMAC-validated against
//                 the stored SecretHash. NEVER logged or returned via API.
//
// REQUEST AUTH (callers send three headers)
//   X-App-Key:        public id
//   X-App-Timestamp:  ISO-8601 UTC, must be within ±5 minutes
//   X-App-Signature:  HMAC-SHA256(Secret, "{X-App-Key}\n{Timestamp}\n{Method}\n{Path}\n{BodySha256}")
//                     base64 of the digest
//
// Replay protection: timestamp check + nonce table (optional later).
// Tenant resolution: every key belongs to one ClientId, so a successful
// validation also resolves the tenant.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;

namespace Platform.Domain.Identity;

[Table("AppApiKeys")]
public class ApiKey : ITenantOwned
{
    [Key]
    public int ApiKeyId { get; set; }

    /// <summary>Human-readable name for the admin UI: "CCPC Portal", "Mobile API".</summary>
    [Required, StringLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Public, indexed. Sent as X-App-Key on every request.</summary>
    [Required, StringLength(64)]
    public string AppKey { get; set; } = string.Empty;

    /// <summary>Salted SHA-256 hash of the secret. The plaintext is only shown once at creation.</summary>
    [Required, StringLength(128)]
    public string SecretHash { get; set; } = string.Empty;

    /// <summary>Comma-separated scope codes: "orders.read,wallet.recharge,menu.read". Empty = full tenant scope.</summary>
    [StringLength(1024)]
    public string? Scopes { get; set; }

    /// <summary>Optional IP allow-list (CIDR, comma-separated). Empty = no restriction.</summary>
    [StringLength(1024)]
    public string? AllowedIps { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAtUtc { get; set; }
    public DateTime? LastUsedAtUtc { get; set; }
    [StringLength(45)] public string? LastUsedIp { get; set; }
    public long UsageCount { get; set; }

    [StringLength(100)] public string? CreatedBy { get; set; }
}
