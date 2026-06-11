// =============================================================================
// RefreshToken  (Platform.Domain.Identity)
// -----------------------------------------------------------------------------
// Rotating refresh-token table. On each /auth/refresh call we revoke the
// presented token and issue a fresh one — so a replay of an old refresh
// token is detected (ReplacedByToken chain). Standard OAuth2 refresh-token
// rotation pattern.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Platform.Domain.Identity;

// NOT ITenantOwned — tenant scoping flows through the parent User.ClientId.
[Table("AppRefreshTokens")]
public class RefreshToken
{
    [Key]
    public long RefreshTokenId { get; set; }

    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    /// <summary>Random opaque 256-bit base64url string. Stored hashed.</summary>
    [Required, StringLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAtUtc { get; set; }
    [StringLength(45)] public string? CreatedIp { get; set; }
    [StringLength(45)] public string? RevokedIp { get; set; }
    [StringLength(500)] public string? ReplacedByToken { get; set; }
    [StringLength(200)] public string? RevocationReason { get; set; }

    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;
}
