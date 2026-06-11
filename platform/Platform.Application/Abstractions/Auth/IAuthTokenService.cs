// =============================================================================
// IAuthTokenService  (Platform.Application.Abstractions.Auth)
// -----------------------------------------------------------------------------
// Single-use, short-lived token issuance for forgot-password / magic-link /
// email-verification flows. Controllers depend on this interface; never on
// IAppDbContext (ADR 0004).
// =============================================================================

namespace Platform.Application.Abstractions.Auth;

public sealed record AuthTokenIssueResult(string RawToken, DateTime ExpiresAtUtc);
public sealed record AuthTokenRedeemResult(int UserId, string Purpose);

public interface IAuthTokenService
{
    /// <summary>Mint a fresh token for the given user + purpose. Existing unconsumed tokens for the same purpose are revoked.</summary>
    Task<AuthTokenIssueResult> IssueAsync(int userId, string purpose, TimeSpan ttl, string? ipAddress = null, CancellationToken cancellationToken = default);

    /// <summary>Verify + consume a token atomically. Returns the UserId on success.</summary>
    Task<AuthTokenRedeemResult?> RedeemAsync(string rawToken, string purpose, CancellationToken cancellationToken = default);
}
