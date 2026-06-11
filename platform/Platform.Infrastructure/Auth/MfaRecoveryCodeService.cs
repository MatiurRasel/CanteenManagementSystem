// =============================================================================
// MfaRecoveryCodeService  (Platform.Infrastructure.Auth)
// -----------------------------------------------------------------------------
// PBKDF2-backed recovery-code persistence. Default impl of
// IMfaRecoveryCodeService. Wired in Platform.Presentation AuthExtensions.
//
// LAYERING (per ADR 0004 — strict)
//   * Controllers do NOT use IAppDbContext directly.
//   * Services use IRepository<T> via IUnitOfWork.Repository<T>() and
//     persist with IUnitOfWork.SaveChangesAsync().
//   * Only repositories (and explicitly CQRS handlers inside Application)
//     reach for the DbContext.
//
// CODE SHAPE
//   * 5+5 alphanumeric, separated by hyphen: "5F2H7-K9XYM".
//   * Uses Crockford-style alphabet (0-9 + A-Z minus I/L/O/U) — easy to read
//     off a printed sheet and unambiguous in scanning.
//   * PBKDF2-HMAC-SHA256 with 50 000 iterations and a 16-byte salt per code.
// =============================================================================

using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions.Auth;
using Platform.Application.Persistence;
using Platform.Domain.Identity;

namespace Platform.Infrastructure.Auth;

public sealed class MfaRecoveryCodeService : IMfaRecoveryCodeService
{
    // Crockford-ish alphabet — no I, L, O, U.
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int    Pbkdf2Iterations = 50_000;
    private const int    SaltBytes = 16;
    private const int    HashBytes = 32;

    private readonly IUnitOfWork _uow;

    public MfaRecoveryCodeService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<IReadOnlyList<string>> RegenerateAsync(int userId, int count = 8, CancellationToken cancellationToken = default)
    {
        var repo = _uow.Repository<MfaRecoveryCode>();

        // Revoke existing codes — keep history rows for audit by setting
        // ConsumedAtUtc rather than deleting.
        var existing = await repo.Query()
            .Where(c => c.UserId == userId && c.ConsumedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var row in existing)
        {
            row.ConsumedAtUtc = DateTime.UtcNow;
            repo.Update(row);
        }

        var raw = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var code = GenerateCode();
            var (salt, hash) = HashCode(code);
            await repo.AddAsync(new MfaRecoveryCode
            {
                UserId       = userId,
                CodeHash     = hash,
                Salt         = salt,
                CodePrefix   = code[..2],
                IssuedAtUtc  = DateTime.UtcNow,
            }, cancellationToken);
            raw.Add(code);
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return raw;
    }

    public async Task<bool> VerifyAndConsumeAsync(int userId, string rawCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawCode)) return false;
        var normalised = NormaliseCode(rawCode);
        if (normalised.Length < 8) return false;       // ignore TOTP-shaped input

        var repo = _uow.Repository<MfaRecoveryCode>();
        var candidates = await repo.Query()
            .Where(c => c.UserId == userId && c.ConsumedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            if (Verify(normalised, candidate.Salt, candidate.CodeHash))
            {
                candidate.ConsumedAtUtc = DateTime.UtcNow;
                repo.Update(candidate);
                await _uow.SaveChangesAsync(cancellationToken);
                return true;
            }
        }
        return false;
    }

    public Task<int> RemainingAsync(int userId, CancellationToken cancellationToken = default)
        => _uow.Repository<MfaRecoveryCode>().Query()
            .CountAsync(c => c.UserId == userId && c.ConsumedAtUtc == null, cancellationToken);

    // ─── helpers ──────────────────────────────────────────────────────────

    private static string GenerateCode()
    {
        var sb = new StringBuilder(11);
        for (var i = 0; i < 10; i++)
        {
            if (i == 5) sb.Append('-');
            sb.Append(Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]);
        }
        return sb.ToString();
    }

    private static string NormaliseCode(string raw)
        => new string(raw.Where(c => c != '-' && c != ' ').Select(char.ToUpperInvariant).ToArray());

    private static (string saltHex, string hashHex) HashCode(string raw)
    {
        var normalised = NormaliseCode(raw);
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(normalised, salt, Pbkdf2Iterations,
            HashAlgorithmName.SHA256, HashBytes);
        return (Convert.ToHexString(salt), Convert.ToHexString(hash));
    }

    private static bool Verify(string normalisedRaw, string saltHex, string expectedHashHex)
    {
        var salt = Convert.FromHexString(saltHex);
        var expected = Convert.FromHexString(expectedHashHex);
        var actual = Rfc2898DeriveBytes.Pbkdf2(normalisedRaw, salt, Pbkdf2Iterations,
            HashAlgorithmName.SHA256, HashBytes);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
