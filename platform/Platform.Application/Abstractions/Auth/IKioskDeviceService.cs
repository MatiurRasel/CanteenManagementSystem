// =============================================================================
// IKioskDeviceService  (Platform.Application.Abstractions.Auth)
// -----------------------------------------------------------------------------
// Issue, list, redeem and revoke kiosk pairing tokens. Controllers depend on
// this interface; they never touch the DbContext directly (ADR 0004).
// =============================================================================

namespace Platform.Application.Abstractions.Auth;

public sealed record KioskIssueResult(int KioskDeviceId, string Name, string RawToken, string PairUrl);
public sealed record KioskListItem(int KioskDeviceId, string Name, string TokenPrefix, bool IsActive,
                                   DateTime IssuedAtUtc, DateTime? RedeemedAtUtc, DateTime? LastSeenAtUtc,
                                   string? LastSeenIp);
public sealed record KioskRedeemResult(int KioskDeviceId, string Name, int ClientId);

public interface IKioskDeviceService
{
    /// <summary>Create a new kiosk device row + mint a one-time raw token.</summary>
    /// <returns>The raw token (shown to the admin once) and a deep-link URL.</returns>
    Task<KioskIssueResult> IssueAsync(string name, int issuedByUserId, string baseUrl, CancellationToken cancellationToken = default);

    /// <summary>Verify a raw token; on success stamp RedeemedAtUtc + return the device claim.</summary>
    Task<KioskRedeemResult?> RedeemAsync(string rawToken, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>Update LastSeenAtUtc / LastSeenIp on every page load — best-effort.</summary>
    Task TouchAsync(int kioskDeviceId, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>Disable a paired device. Future cookie validations log it out.</summary>
    Task RevokeAsync(int kioskDeviceId, CancellationToken cancellationToken = default);

    /// <summary>List all devices in the current tenant for the admin UI.</summary>
    Task<IReadOnlyList<KioskListItem>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>True iff the device exists, is active, and belongs to the current tenant.</summary>
    Task<bool> IsActiveAsync(int kioskDeviceId, CancellationToken cancellationToken = default);
}
