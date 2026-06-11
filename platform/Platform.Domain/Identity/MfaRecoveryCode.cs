// =============================================================================
// MfaRecoveryCode  (Platform.Domain.Identity)
// -----------------------------------------------------------------------------
// One-time recovery codes for users who have lost access to their TOTP app.
// Generated on demand (or after every MFA enrolment); stored ONLY as a hash
// (SHA-256 of the raw code + per-user salt) so a DB leak is not a backdoor.
//
// LIFECYCLE
//   1. User enrols MFA → RegenerateRecoveryCodes() is called → 8 codes minted.
//   2. UI shows the codes ONCE (the only chance to write them down).
//   3. On login, if the entered "code" is a recovery code (not a 6-digit TOTP),
//      we hash it, look it up, mark ConsumedAtUtc, and allow sign-in.
//   4. Once consumed a code is dead. When the unused remaining count drops
//      below 3, the UI prompts the user to regenerate a new batch.
//
// HASHING
//   * Algorithm: PBKDF2-HMAC-SHA256, 50 000 iterations, 16-byte salt.
//   * Salt stored alongside the hash so verification is constant-time.
//   * Code format: 5 + 5 alphanumeric (Crockford base32, no ambiguous chars),
//                  e.g. "5F2H7-K9XYM". Easy to read off a piece of paper.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Platform.Domain.Identity;

[Table("AppMfaRecoveryCodes")]
public class MfaRecoveryCode
{
    [Key]
    public int RecoveryCodeId { get; set; }

    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    /// <summary>Hex-encoded PBKDF2 hash of the raw code + Salt.</summary>
    [Required, StringLength(128)]
    public string CodeHash { get; set; } = string.Empty;

    /// <summary>Hex-encoded 16-byte random salt.</summary>
    [Required, StringLength(64)]
    public string Salt { get; set; } = string.Empty;

    /// <summary>First 2 chars of the raw code — UI hint ("starts with 5F…") for matching to a written copy.</summary>
    [Required, StringLength(2)]
    public string CodePrefix { get; set; } = string.Empty;

    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ConsumedAtUtc { get; set; }
}
