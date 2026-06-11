// =============================================================================
// IAuthService  (Platform.Application.Abstractions.Auth)
// -----------------------------------------------------------------------------
// Coordinates the login + refresh + revoke flows. Stateless service; calls
// out to IPasswordHasher, ITokenIssuer, and the AppUsers / AppRefreshTokens
// tables.
//
// FLOWS
//   LoginAsync         → AuthTokenPair on success, locks out after N failures.
//   RefreshAsync       → Rotates the refresh token; old one is revoked.
//   LogoutAsync        → Revokes the presented refresh token.
//   RevokeAllAsync     → "Sign out everywhere" — admin or after password change.
// =============================================================================

using Platform.Application.Results;

namespace Platform.Application.Abstractions.Auth;

public interface IAuthService
{
    Task<Result<AuthTokenPair>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<Result<AuthTokenPair>> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken = default);
    Task<Result> LogoutAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken = default);
    Task<Result> RevokeAllForUserAsync(int userId, string reason, CancellationToken cancellationToken = default);

    /// <summary>Change a user's password. Always rotates RefreshTokens for safety.</summary>
    Task<Result> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hydrate the full profile (with roles + permissions) for an already-authenticated user.
    /// Used by post-MFA cookie sign-in and by /me-style endpoints — controllers must NOT
    /// reach into the DbContext for this data (see ADR 0004).
    /// </summary>
    Task<Result<UserProfileDto>> GetProfileAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Look up a user by email — used by forgot-password / magic-link to mint a token.
    /// Returns success even if the email is unknown (caller treats either case identically
    /// to avoid leaking which addresses are registered).
    /// </summary>
    Task<Result<int?>> FindUserIdByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set a new password without requiring the current one. Used by the password-reset
    /// flow after the user has presented a valid AuthToken. Always rotates RefreshTokens.
    /// </summary>
    Task<Result> ResetPasswordAsync(int userId, string newPassword, CancellationToken cancellationToken = default);
}

public sealed record LoginRequest(string UserName, string Password, string? IpAddress);

public sealed record AuthTokenPair(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    UserProfileDto User);

public sealed record UserProfileDto(
    int UserId,
    string UserName,
    string DisplayName,
    string? Email,
    int? ClientId,
    string ClientCode,
    bool MustChangePassword,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);
