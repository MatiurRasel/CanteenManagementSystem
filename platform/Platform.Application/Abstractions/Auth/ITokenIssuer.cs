// =============================================================================
// ITokenIssuer  (Platform.Application.Abstractions.Auth)
// -----------------------------------------------------------------------------
// Stateless JWT issuer. Pairs with a refresh-token store for rotation.
//   * Access token: HS256, ~15 minutes, carries user id + roles + tenant.
//   * Refresh token: opaque 256-bit base64url, persisted hashed in
//     AppRefreshTokens, rotated on every /auth/refresh.
//
// Symmetric (HS256) is chosen over asymmetric (RS256) because every service
// that issues OR verifies tokens runs inside the same trust boundary (we own
// the platform). If you start federating verification to third parties,
// switch to RS256 + public-key endpoint.
// =============================================================================

namespace Platform.Application.Abstractions.Auth;

public interface ITokenIssuer
{
    /// <summary>Issue a fresh access-token JWT for the given user.</summary>
    string IssueAccessToken(TokenSubject subject);

    /// <summary>Generate a new refresh token (caller is responsible for persisting the hash).</summary>
    (string PlainToken, string HashedToken, DateTime ExpiresAtUtc) IssueRefreshToken();

    /// <summary>Validate an access token's signature + lifetime. Returns the parsed subject if valid.</summary>
    TokenSubject? Validate(string accessToken);
}

public sealed record TokenSubject(
    int UserId,
    string UserName,
    string DisplayName,
    int ClientId,
    string ClientCode,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
