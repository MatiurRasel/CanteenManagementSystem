// =============================================================================
// AuthTokenService  (Platform.Infrastructure.Auth)
// -----------------------------------------------------------------------------
// PBKDF2-backed single-use token store. Same hashing pattern as
// MfaRecoveryCodeService + KioskDeviceService.
//
// LAYERING (ADR 0004): IUnitOfWork.Repository<T>() only, no IAppDbContext.
// =============================================================================

using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions.Auth;
using Platform.Application.Persistence;
using Platform.Domain.Identity;

namespace Platform.Infrastructure.Auth;

public sealed class AuthTokenService : IAuthTokenService
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int    Pbkdf2Iterations = 50_000;
    private const int    SaltBytes = 16;
    private const int    HashBytes = 32;
    private const int    TokenChars = 32;

    private readonly IUnitOfWork _uow;

    public AuthTokenService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<AuthTokenIssueResult> IssueAsync(int userId, string purpose, TimeSpan ttl, string? ipAddress = null, CancellationToken cancellationToken = default)
    {
        // Revoke any unconsumed tokens for the same (user, purpose) so the
        // most-recent reset link is always the live one.
        var existing = await _uow.Repository<AuthToken>().Query()
            .Where(t => t.UserId == userId && t.Purpose == purpose && t.ConsumedAtUtc == null)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var stale in existing)
        {
            stale.ConsumedAtUtc = now;
            _uow.Repository<AuthToken>().Update(stale);
        }

        var raw = GenerateToken();
        var (salt, hash) = HashToken(raw);

        await _uow.Repository<AuthToken>().AddAsync(new AuthToken
        {
            UserId       = userId,
            Purpose      = purpose,
            TokenHash    = hash,
            Salt         = salt,
            IssuedAtUtc  = now,
            ExpiresAtUtc = now.Add(ttl),
            IssuedFromIp = ipAddress,
        }, cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);
        return new AuthTokenIssueResult(raw, now.Add(ttl));
    }

    public async Task<AuthTokenRedeemResult?> RedeemAsync(string rawToken, string purpose, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;
        var normalised = Normalise(rawToken);
        if (normalised.Length < 16) return null;

        var now = DateTime.UtcNow;
        var candidates = await _uow.Repository<AuthToken>().Query()
            .Where(t => t.Purpose == purpose && t.ConsumedAtUtc == null && t.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);

        foreach (var token in candidates)
        {
            if (!Verify(normalised, token.Salt, token.TokenHash)) continue;
            token.ConsumedAtUtc = now;
            _uow.Repository<AuthToken>().Update(token);
            await _uow.SaveChangesAsync(cancellationToken);
            return new AuthTokenRedeemResult(token.UserId, token.Purpose);
        }
        return null;
    }

    // ─── helpers ───────────────────────────────────────────────────────────

    private static string GenerateToken()
    {
        var sb = new StringBuilder(TokenChars);
        for (var i = 0; i < TokenChars; i++)
            sb.Append(Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]);
        return sb.ToString();
    }

    private static string Normalise(string raw)
        => new string(raw.Where(c => c != '-' && c != ' ').Select(char.ToUpperInvariant).ToArray());

    private static (string saltHex, string hashHex) HashToken(string raw)
    {
        var n = Normalise(raw);
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(n, salt, Pbkdf2Iterations,
            HashAlgorithmName.SHA256, HashBytes);
        return (Convert.ToHexString(salt), Convert.ToHexString(hash));
    }

    private static bool Verify(string normalised, string saltHex, string expectedHashHex)
    {
        var salt = Convert.FromHexString(saltHex);
        var expected = Convert.FromHexString(expectedHashHex);
        var actual = Rfc2898DeriveBytes.Pbkdf2(normalised, salt, Pbkdf2Iterations,
            HashAlgorithmName.SHA256, HashBytes);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
