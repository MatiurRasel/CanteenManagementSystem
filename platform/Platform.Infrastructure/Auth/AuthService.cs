// =============================================================================
// AuthService  (Platform.Infrastructure.Auth)
// -----------------------------------------------------------------------------
// Implements IAuthService. End-to-end login/refresh/logout/change-password
// with:
//   * BCrypt password verification + opportunistic rehash on login.
//   * Lockout after N consecutive failures (configurable via AuthOptions).
//   * Rotating refresh tokens stored as SHA-256 hashes; reuse detection.
//   * Tenant binding: token claims carry user.ClientId (NOT the request's
//     resolved tenant) so a TenantAdmin signed-in from a wrong subdomain
//     still gets scoped to their own tenant downstream.
//   * Audit-trail integration on every login/refresh/revoke event.
//
// LAYERING (ADR 0004, Batch 2)
//   Depends on IUnitOfWork for the user + refresh-token writes, and
//   IReadOnlyRepository<Client> for the tenant-active check (read-only).
//   No IAppDbContext.
//
// MULTI-TENANT LOGIN
//   Users are NOT ITenantOwned. Login query uses IgnoreQueryFilters via the
//   repo's Query() to find the user by name regardless of the request's
//   tenant context. The user's own ClientId then drives downstream scope.
// =============================================================================

using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Application.Abstractions.Audit;
using Platform.Application.Abstractions.Auth;
using Platform.Application.Abstractions.Time;
using Platform.Application.Configuration;
using Platform.Application.Persistence;
using Platform.Application.Results;
using Platform.Domain.Identity;
using Platform.Domain.Tenancy;

namespace Platform.Infrastructure.Auth;

public sealed class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly IReadOnlyRepository<Client> _clients;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenIssuer _tokens;
    private readonly IAuditTrail _audit;
    private readonly IClock _clock;
    private readonly AuthOptions _options;
    private readonly TenancyOptions _tenancy;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUnitOfWork uow,
        IReadOnlyRepository<Client> clients,
        IPasswordHasher hasher, ITokenIssuer tokens,
        IAuditTrail audit, IClock clock,
        IOptions<AuthOptions> options, IOptions<TenancyOptions> tenancy,
        ILogger<AuthService> logger)
    {
        _uow = uow; _clients = clients;
        _hasher = hasher; _tokens = tokens; _audit = audit;
        _clock = clock; _options = options.Value; _tenancy = tenancy.Value;
        _logger = logger;
    }

    private IRepository<User>         Users         => _uow.Repository<User>();
    private IRepository<RefreshToken> RefreshTokens => _uow.Repository<RefreshToken>();

    public async Task<Result<AuthTokenPair>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters: UserName is globally unique; we MUST find the user
        // regardless of the request's resolved tenant. The user's own ClientId
        // then drives downstream tenant scope.
        var user = await Users.Query()
            .IgnoreQueryFilters()
            .Include(u => u.Roles).ThenInclude(r => r.Role).ThenInclude(r => r!.Permissions).ThenInclude(p => p.Permission)
            .FirstOrDefaultAsync(u => u.UserName == request.UserName, cancellationToken);

        if (user is null)
        {
            // Identical response shape to wrong-password to avoid username enumeration.
            return Result.Failure<AuthTokenPair>(Error.Unauthorized("Invalid credentials."));
        }
        if (!user.IsActive) return Result.Failure<AuthTokenPair>(Error.Unauthorized("Account inactive."));
        if (user.LockedOutUntilUtc is { } until && until > _clock.UtcNow)
        {
            return Result.Failure<AuthTokenPair>(Error.Unauthorized($"Locked. Try again after {until:u}."));
        }

        if (!_hasher.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= _options.MaxLoginAttempts)
            {
                user.LockedOutUntilUtc = _clock.UtcNow.AddMinutes(_options.LockoutMinutes);
                user.FailedLoginCount = 0;
            }
            Users.Update(user);
            await _uow.SaveChangesAsync(cancellationToken);
            await _audit.RecordAsync("Auth.LoginFailed", "User", user.UserId.ToString(), new { request.UserName }, cancellationToken);
            return Result.Failure<AuthTokenPair>(Error.Unauthorized("Invalid credentials."));
        }

        // If the user is bound to a tenant, that tenant MUST be active.
        // SystemAdmin (ClientId=null) skips this check — they govern tenants.
        string clientCode = _tenancy.DefaultClientCode;
        if (user.ClientId is int boundTenantId)
        {
            var tenant = await _clients.Query()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.ClientId == boundTenantId, cancellationToken);

            if (tenant is null) return Result.Failure<AuthTokenPair>(Error.Unauthorized("Account's tenant no longer exists."));
            if (!tenant.IsActive) return Result.Failure<AuthTokenPair>(Error.Unauthorized("Account's tenant is suspended."));
            clientCode = tenant.ClientCode;
        }

        // Successful login: reset lockout, upgrade hash if needed.
        user.FailedLoginCount = 0;
        user.LockedOutUntilUtc = null;
        user.LastLoginAtUtc = _clock.UtcNow;
        user.LastLoginIp = request.IpAddress;
        if (_hasher.NeedsRehash(user.PasswordHash))
        {
            user.PasswordHash = _hasher.Hash(request.Password);
        }
        Users.Update(user);

        var pair = await IssueTokenPairAsync(user, clientCode, request.IpAddress, cancellationToken);
        await _audit.RecordAsync("Auth.LoginOk", "User", user.UserId.ToString(), new { user.UserName }, cancellationToken);
        return Result.Success(pair);
    }

    public async Task<Result<AuthTokenPair>> RefreshAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var hash = HashRefreshToken(refreshToken);
        var existing = await RefreshTokens.Query()
            .IgnoreQueryFilters()
            .Include(t => t.User).ThenInclude(u => u!.Roles).ThenInclude(r => r.Role).ThenInclude(r => r!.Permissions).ThenInclude(p => p.Permission)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (existing is null) return Result.Failure<AuthTokenPair>(Error.Unauthorized("Unknown refresh token."));
        if (!existing.IsActive)
        {
            // Reuse of a revoked token is a strong signal of theft — kill all sessions for this user.
            await RevokeAllForUserAsync(existing.UserId, "Refresh-token reuse detected", cancellationToken);
            return Result.Failure<AuthTokenPair>(Error.Unauthorized("Refresh token reuse detected; all sessions revoked."));
        }

        existing.RevokedAtUtc = _clock.UtcNow;
        existing.RevokedIp = ipAddress;
        existing.RevocationReason = "Rotated";
        RefreshTokens.Update(existing);

        // Re-resolve the user's tenant code (admin may have renamed the Client).
        var clientCode = _tenancy.DefaultClientCode;
        if (existing.User!.ClientId is int tid)
        {
            var t = await _clients.Query().IgnoreQueryFilters().FirstOrDefaultAsync(c => c.ClientId == tid, cancellationToken);
            if (t is { IsActive: true }) clientCode = t.ClientCode;
            else return Result.Failure<AuthTokenPair>(Error.Unauthorized("Tenant inactive."));
        }

        var pair = await IssueTokenPairAsync(existing.User!, clientCode, ipAddress, cancellationToken);
        existing.ReplacedByToken = pair.RefreshToken;
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success(pair);
    }

    public async Task<Result> LogoutAsync(string refreshToken, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var hash = HashRefreshToken(refreshToken);
        var existing = await RefreshTokens.Query()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (existing is null) return Result.Success(); // idempotent
        existing.RevokedAtUtc = _clock.UtcNow;
        existing.RevokedIp = ipAddress;
        existing.RevocationReason = "Logout";
        RefreshTokens.Update(existing);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> RevokeAllForUserAsync(int userId, string reason, CancellationToken cancellationToken = default)
    {
        var tokens = await RefreshTokens.Query().IgnoreQueryFilters()
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null).ToListAsync(cancellationToken);
        foreach (var t in tokens)
        {
            t.RevokedAtUtc = _clock.UtcNow;
            t.RevocationReason = reason;
        }
        RefreshTokens.UpdateRange(tokens);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ChangePasswordAsync(int userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await Users.Query().IgnoreQueryFilters().FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user is null) return Result.Failure(Error.NotFound("User not found."));
        if (!_hasher.Verify(currentPassword, user.PasswordHash))
        {
            return Result.Failure(Error.Unauthorized("Current password is incorrect."));
        }
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            return Result.Failure(Error.Validation("New password must be at least 8 characters."));
        }
        user.PasswordHash = _hasher.Hash(newPassword);
        user.MustChangePassword = false;
        Users.Update(user);
        await _uow.SaveChangesAsync(cancellationToken);
        await RevokeAllForUserAsync(userId, "Password changed", cancellationToken);
        await _audit.RecordAsync("Auth.PasswordChanged", "User", userId.ToString(), null, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<int?>> FindUserIdByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return Result.Success<int?>(null);
        var normalised = email.Trim().ToLowerInvariant();
        var userId = await Users.Query().IgnoreQueryFilters()
            .Where(u => u.Email != null && u.Email.ToLower() == normalised && u.IsActive)
            .Select(u => (int?)u.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        return Result.Success(userId);
    }

    public async Task<Result> ResetPasswordAsync(int userId, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            return Result.Failure(Error.Validation("New password must be at least 8 characters."));
        }
        var user = await Users.Query().IgnoreQueryFilters().FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user is null) return Result.Failure(Error.NotFound("User not found."));

        user.PasswordHash = _hasher.Hash(newPassword);
        user.MustChangePassword = false;
        user.FailedLoginCount = 0;
        user.LockedOutUntilUtc = null;
        Users.Update(user);
        await _uow.SaveChangesAsync(cancellationToken);
        await RevokeAllForUserAsync(userId, "Password reset via token", cancellationToken);
        await _audit.RecordAsync("Auth.PasswordReset", "User", userId.ToString(), null, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<UserProfileDto>> GetProfileAsync(int userId, CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters: users are global (not ITenantOwned). Same pattern as LoginAsync.
        var user = await Users.NoTrackingQuery()
            .IgnoreQueryFilters()
            .Include(u => u.Roles).ThenInclude(r => r.Role).ThenInclude(r => r!.Permissions).ThenInclude(p => p.Permission)
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user is null) return Result.Failure<UserProfileDto>(Error.NotFound("User not found."));

        var roles = user.Roles.Select(r => r.Role!.RoleCode).ToList();
        var perms = user.Roles.SelectMany(r => r.Role!.Permissions.Select(p => p.Permission!.PermissionCode)).Distinct().ToList();
        return Result.Success(new UserProfileDto(
            user.UserId, user.UserName, user.DisplayName, user.Email,
            user.ClientId, user.Tenant?.ClientCode ?? string.Empty, user.MustChangePassword,
            roles, perms));
    }

    // -------------------- helpers ------------------------------------------
    private async Task<AuthTokenPair> IssueTokenPairAsync(User user, string clientCode, string? ipAddress, CancellationToken cancellationToken)
    {
        var roles = user.Roles.Select(r => r.Role!.RoleCode).ToList();
        var perms = user.Roles.SelectMany(r => r.Role!.Permissions.Select(p => p.Permission!.PermissionCode)).Distinct().ToList();

        // ClientId for the JWT claim: user's own ClientId, or DefaultClientId
        // for SystemAdmin (so downstream services have *some* tenant to scope to;
        // SystemAdmin's role flag is the authoritative cross-tenant signal).
        var tenantIdForToken = user.ClientId ?? _tenancy.DefaultClientId;

        var subject = new TokenSubject(
            user.UserId, user.UserName, user.DisplayName,
            tenantIdForToken, clientCode, roles, perms);
        var access  = _tokens.IssueAccessToken(subject);
        var (plain, hash, refreshExp) = _tokens.IssueRefreshToken();

        await RefreshTokens.AddAsync(new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = hash,
            ExpiresAtUtc = refreshExp,
            CreatedAtUtc = _clock.UtcNow,
            CreatedIp = ipAddress
        }, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return new AuthTokenPair(
            AccessToken: access,
            AccessTokenExpiresAtUtc: _clock.UtcNow.AddMinutes(_options.AccessTokenMinutes),
            RefreshToken: plain,
            RefreshTokenExpiresAtUtc: refreshExp,
            User: new UserProfileDto(
                user.UserId, user.UserName, user.DisplayName, user.Email,
                user.ClientId, clientCode, user.MustChangePassword,
                roles, perms));
    }

    private static string HashRefreshToken(string plain)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plain)));
}
