// =============================================================================
// IPasswordHasher  (Platform.Application.Abstractions.Auth)
// -----------------------------------------------------------------------------
// Single seam over the password-hashing primitive. Default implementation is
// BCrypt with work factor 11 (~250 ms per hash on modern hardware).
// =============================================================================

namespace Platform.Application.Abstractions.Auth;

public interface IPasswordHasher
{
    /// <summary>Hash a plaintext password with a fresh per-password salt.</summary>
    string Hash(string plaintext);

    /// <summary>Verify a plaintext against a stored hash. Constant-time.</summary>
    bool Verify(string plaintext, string hash);

    /// <summary>True when the stored hash uses an out-of-date work factor and should be re-hashed on the next successful login.</summary>
    bool NeedsRehash(string hash);
}
