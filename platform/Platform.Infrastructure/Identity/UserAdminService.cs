// =============================================================================
// UserAdminService  (Platform.Infrastructure.Identity)
// -----------------------------------------------------------------------------
// IUserAdminService default impl. IUnitOfWork only — no IAppDbContext.
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions.Auth;
using Platform.Application.Abstractions.Identity;
using Platform.Application.Persistence;
using Platform.Application.Results;
using Platform.Domain.Identity;

namespace Platform.Infrastructure.Identity;

public sealed class UserAdminService : IUserAdminService
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;
    private readonly IAuthService _auth;

    public UserAdminService(IUnitOfWork uow, IPasswordHasher hasher, IAuthService auth)
    {
        _uow = uow; _hasher = hasher; _auth = auth;
    }

    private IRepository<User>     Users     => _uow.Repository<User>();
    private IRepository<UserRole> UserRoles => _uow.Repository<UserRole>();
    private IRepository<Role>     Roles     => _uow.Repository<Role>();

    public async Task<IReadOnlyList<UserListItem>> ListAsync(bool crossTenant, CancellationToken ct = default)
    {
        IQueryable<User> q = Users.NoTrackingQuery().Include(u => u.Roles).ThenInclude(r => r.Role);
        if (crossTenant) q = q.IgnoreQueryFilters();
        var users = await q.OrderBy(u => u.UserName).ToListAsync(ct);
        return users.Select(u => new UserListItem(
            u.UserId, u.ClientId, u.UserName, u.DisplayName, u.Email, u.UserKind,
            u.IsActive, u.LastLoginAtUtc,
            u.Roles.Select(r => r.Role!.RoleCode).ToList())).ToList();
    }

    public async Task<IReadOnlyList<Role>> ListAssignableRolesAsync(CancellationToken ct = default)
        => await Roles.NoTrackingQuery().Where(r => !r.IsSystem).OrderBy(r => r.RoleCode).ToListAsync(ct);

    public async Task<Result<InviteUserOutcome>> InviteAsync(InviteUserInput input, CancellationToken ct = default)
    {
        var userName = input.UserName.Trim().ToLowerInvariant();
        if (await Users.Query().IgnoreQueryFilters().AnyAsync(u => u.UserName == userName, ct))
            return Result.Failure<InviteUserOutcome>(Error.Conflict("That user name already exists."));

        var temp = $"Change@{Guid.NewGuid().ToString("N")[..8]}";
        var user = new User
        {
            ClientId           = input.BoundClientId,
            UserName           = userName,
            DisplayName        = input.DisplayName.Trim(),
            Email              = input.Email.Trim(),
            UserKind           = input.UserKind.Trim(),
            PasswordHash       = _hasher.Hash(temp),
            MustChangePassword = true,
            IsActive           = true,
            CreatedAtUtc       = DateTime.UtcNow
        };
        await Users.AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        var role = await Roles.FirstOrDefaultAsync(r => r.RoleCode == input.RoleCode, ct);
        if (role is not null)
        {
            await UserRoles.AddAsync(new UserRole { UserId = user.UserId, RoleId = role.RoleId }, ct);
            await _uow.SaveChangesAsync(ct);
        }
        return Result.Success(new InviteUserOutcome(user, temp));
    }

    public async Task<Result<User>> ToggleActiveAsync(int userId, bool requesterIsSystemAdmin, int? requesterClientId, CancellationToken ct = default)
    {
        var user = await Users.Query().IgnoreQueryFilters().FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user is null) return Result.Failure<User>(Error.NotFound("User not found."));
        if (!CanManage(user, requesterIsSystemAdmin, requesterClientId))
            return Result.Failure<User>(Error.Unauthorized("Cannot manage a user in another tenant."));

        user.IsActive = !user.IsActive;
        Users.Update(user);
        await _uow.SaveChangesAsync(ct);
        if (!user.IsActive) await _auth.RevokeAllForUserAsync(user.UserId, "Admin lock", ct);
        return Result.Success(user);
    }

    public async Task<Result<ResetPasswordOutcome>> ResetPasswordAsync(int userId, bool requesterIsSystemAdmin, int? requesterClientId, CancellationToken ct = default)
    {
        var user = await Users.Query().IgnoreQueryFilters().FirstOrDefaultAsync(u => u.UserId == userId, ct);
        if (user is null) return Result.Failure<ResetPasswordOutcome>(Error.NotFound("User not found."));
        if (!CanManage(user, requesterIsSystemAdmin, requesterClientId))
            return Result.Failure<ResetPasswordOutcome>(Error.Unauthorized("Cannot manage a user in another tenant."));

        var temp = $"Reset@{Guid.NewGuid().ToString("N")[..8]}";
        user.PasswordHash       = _hasher.Hash(temp);
        user.MustChangePassword = true;
        user.FailedLoginCount   = 0;
        user.LockedOutUntilUtc  = null;
        Users.Update(user);
        await _uow.SaveChangesAsync(ct);
        await _auth.RevokeAllForUserAsync(user.UserId, "Password reset by admin", ct);
        return Result.Success(new ResetPasswordOutcome(user, temp));
    }

    public async Task<Result<BulkImportOutcome>> BulkImportAsync(IEnumerable<BulkImportRow> rows, int? boundClientId, CancellationToken ct = default)
    {
        var created = 0; var skipped = 0;
        var samples = new List<(string, string)>();

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.UserName) || string.IsNullOrWhiteSpace(row.DisplayName)) continue;
            var userName = row.UserName.Trim().ToLowerInvariant();
            if (await Users.Query().IgnoreQueryFilters().AnyAsync(u => u.UserName == userName, ct))
            { skipped++; continue; }

            var temp = $"Bulk@{Guid.NewGuid().ToString("N")[..8]}";
            var user = new User
            {
                ClientId           = boundClientId,
                UserName           = userName,
                DisplayName        = row.DisplayName.Trim(),
                Email              = row.Email,
                UserKind           = string.IsNullOrWhiteSpace(row.UserKind) ? "Operator" : row.UserKind!,
                LinkedPersonId     = row.ExternalId,
                PasswordHash       = _hasher.Hash(temp),
                MustChangePassword = true,
                IsActive           = true,
                CreatedAtUtc       = DateTime.UtcNow
            };
            await Users.AddAsync(user, ct);
            await _uow.SaveChangesAsync(ct);

            var roleCode = string.IsNullOrWhiteSpace(row.RoleCode) ? "Operator" : row.RoleCode!;
            var role = await Roles.FirstOrDefaultAsync(r => r.RoleCode == roleCode, ct);
            if (role is not null)
            {
                await UserRoles.AddAsync(new UserRole { UserId = user.UserId, RoleId = role.RoleId }, ct);
                await _uow.SaveChangesAsync(ct);
            }
            created++;
            if (samples.Count < 5) samples.Add((user.UserName, temp));
        }
        return Result.Success(new BulkImportOutcome(created, skipped, samples));
    }

    private static bool CanManage(User target, bool requesterIsSystemAdmin, int? requesterClientId)
        => requesterIsSystemAdmin || (requesterClientId.HasValue && target.ClientId == requesterClientId);
}
