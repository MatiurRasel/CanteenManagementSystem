// =============================================================================
// AuthToken  (Platform.Domain.Identity)
// -----------------------------------------------------------------------------
// Single-use, short-lived token bound to a user. Powers:
//
//   Purpose = "PasswordReset"     → /Account/ResetPassword?token=…
//   Purpose = "MagicLink"         → /Account/MagicLink/Consume?token=…
//   Purpose = "EmailVerification" → /Account/VerifyEmail?token=…  (future)
//
// Stored only as a PBKDF2 hash + salt. Tokens are 32 chars Crockford alphabet
// (~160 bits of entropy) — comfortable for emailing.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Platform.Domain.Identity;

[Table("AppAuthTokens")]
public class AuthToken
{
    [Key]
    public long AuthTokenId { get; set; }

    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    /// <summary>"PasswordReset" / "MagicLink" / "EmailVerification" — see AuthTokenPurpose.</summary>
    [Required, StringLength(32)]
    public string Purpose { get; set; } = string.Empty;

    [Required, StringLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string Salt { get; set; } = string.Empty;

    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }

    [StringLength(45)]
    public string? IssuedFromIp { get; set; }
}

public static class AuthTokenPurpose
{
    public const string PasswordReset     = "PasswordReset";
    public const string MagicLink         = "MagicLink";
    public const string EmailVerification = "EmailVerification";
}
