// =============================================================================
// BCryptPasswordHasher  (Platform.Infrastructure.Auth)
// -----------------------------------------------------------------------------
// Uses BCrypt.Net-Next. Work factor 11 ≈ 250 ms per hash on commodity x64.
// Bump the factor as hardware gets faster — NeedsRehash() flags hashes
// stored with a lower work factor so the next successful login can upgrade
// them silently.
// =============================================================================

using BCrypt.Net;
using Platform.Application.Abstractions.Auth;

namespace Platform.Infrastructure.Auth;

public sealed class BCryptPasswordHasher : IPasswordHasher
{
    private const int CurrentWorkFactor = 11;

    public string Hash(string plaintext)
        => BCrypt.Net.BCrypt.HashPassword(plaintext, workFactor: CurrentWorkFactor);

    public bool Verify(string plaintext, string hash)
    {
        try { return BCrypt.Net.BCrypt.Verify(plaintext, hash); }
        catch (SaltParseException) { return false; }
    }

    public bool NeedsRehash(string hash)
    {
        // BCrypt format: $2a$11$saltsaltsaltsalt...hashhashhash
        // We re-hash if the stored factor is below the current one.
        try
        {
            var parts = hash.Split('$');
            if (parts.Length < 3) return true;
            return int.TryParse(parts[2], out var factor) && factor < CurrentWorkFactor;
        }
        catch { return true; }
    }
}
