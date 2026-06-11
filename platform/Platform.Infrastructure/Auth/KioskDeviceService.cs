// =============================================================================
// KioskDeviceService  (Platform.Infrastructure.Auth)
// -----------------------------------------------------------------------------
// PBKDF2-backed kiosk pairing token store. Same hashing primitives as
// MfaRecoveryCodeService — single bcrypt-equivalent algorithm + per-row salt.
//
// LAYERING (ADR 0004): uses IUnitOfWork.Repository<T>(), no IAppDbContext.
// =============================================================================

using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions.Auth;
using Platform.Application.Persistence;
using Platform.Domain.Identity;

namespace Platform.Infrastructure.Auth;

public sealed class KioskDeviceService : IKioskDeviceService
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"; // Crockford
    private const int    Pbkdf2Iterations = 50_000;
    private const int    SaltBytes = 16;
    private const int    HashBytes = 32;

    private readonly IUnitOfWork _uow;

    public KioskDeviceService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<KioskIssueResult> IssueAsync(string name, int issuedByUserId, string baseUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) name = $"Kiosk {DateTime.UtcNow:yyyyMMddHHmm}";

        var rawToken = GenerateToken();
        var (salt, hash) = HashToken(rawToken);

        var device = new KioskDevice
        {
            Name        = name.Trim(),
            TokenHash   = hash,
            Salt        = salt,
            TokenPrefix = rawToken[..4],
            IsActive    = true,
            IssuedAtUtc = DateTime.UtcNow,
            IssuedByUserId = issuedByUserId,
        };
        await _uow.Repository<KioskDevice>().AddAsync(device, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        var pairUrl = $"{baseUrl.TrimEnd('/')}/kiosk/pair?token={Uri.EscapeDataString(rawToken)}";
        return new KioskIssueResult(device.KioskDeviceId, device.Name, rawToken, pairUrl);
    }

    public async Task<KioskRedeemResult?> RedeemAsync(string rawToken, string? ipAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;
        var normalised = Normalise(rawToken);
        if (normalised.Length < 16) return null;

        // Iterate active devices and constant-time compare. The set is small
        // (one row per tablet per tenant); good enough.
        var devices = await _uow.Repository<KioskDevice>().Query().IgnoreQueryFilters()
            .Where(d => d.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var device in devices)
        {
            if (!Verify(normalised, device.Salt, device.TokenHash)) continue;

            device.RedeemedAtUtc ??= DateTime.UtcNow;
            device.LastSeenAtUtc = DateTime.UtcNow;
            device.LastSeenIp = ipAddress;
            _uow.Repository<KioskDevice>().Update(device);
            await _uow.SaveChangesAsync(cancellationToken);

            // EF shadow property "ClientId" is on every ITenantOwned row.
            var clientId = (int?)_uow.Repository<KioskDevice>().Query()
                .IgnoreQueryFilters()
                .Where(d => d.KioskDeviceId == device.KioskDeviceId)
                .Select(d => EF.Property<int?>(d, "ClientId"))
                .FirstOrDefault() ?? 0;

            return new KioskRedeemResult(device.KioskDeviceId, device.Name, clientId);
        }
        return null;
    }

    public async Task TouchAsync(int kioskDeviceId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var device = await _uow.Repository<KioskDevice>().Query().IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.KioskDeviceId == kioskDeviceId, cancellationToken);
        if (device is null) return;
        device.LastSeenAtUtc = DateTime.UtcNow;
        device.LastSeenIp = ipAddress;
        _uow.Repository<KioskDevice>().Update(device);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAsync(int kioskDeviceId, CancellationToken cancellationToken = default)
    {
        var device = await _uow.Repository<KioskDevice>().Query()
            .FirstOrDefaultAsync(d => d.KioskDeviceId == kioskDeviceId, cancellationToken);
        if (device is null || !device.IsActive) return;
        device.IsActive = false;
        device.RevokedAtUtc = DateTime.UtcNow;
        _uow.Repository<KioskDevice>().Update(device);
        await _uow.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<KioskListItem>> ListAsync(CancellationToken cancellationToken = default)
        => await _uow.Repository<KioskDevice>().Query()
            .OrderByDescending(d => d.IssuedAtUtc)
            .Select(d => new KioskListItem(
                d.KioskDeviceId, d.Name, d.TokenPrefix, d.IsActive,
                d.IssuedAtUtc, d.RedeemedAtUtc, d.LastSeenAtUtc, d.LastSeenIp))
            .ToListAsync(cancellationToken);

    public Task<bool> IsActiveAsync(int kioskDeviceId, CancellationToken cancellationToken = default)
        => _uow.Repository<KioskDevice>().Query().IgnoreQueryFilters()
            .AnyAsync(d => d.KioskDeviceId == kioskDeviceId && d.IsActive, cancellationToken);

    // ─── helpers ───────────────────────────────────────────────────────────

    private static string GenerateToken()
    {
        // 20 chars (~100 bits of entropy) grouped 5-5-5-5 for readability.
        var sb = new StringBuilder(23);
        for (var i = 0; i < 20; i++)
        {
            if (i > 0 && i % 5 == 0) sb.Append('-');
            sb.Append(Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]);
        }
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
