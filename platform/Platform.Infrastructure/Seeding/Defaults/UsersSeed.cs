// =============================================================================
// UsersSeed  (Platform.Infrastructure.Seeding.Defaults)
// -----------------------------------------------------------------------------
// Order = 30. Runs after ClientsSeed (5), PermissionsSeed (10), RolesSeed (20).
//
// USER → TENANT BINDING
//   ClientId is set explicitly on each seeded user:
//     sysadmin       → NULL (cross-tenant SystemAdmin)
//     smartadmin     → default tenant (TenancyOptions.DefaultClientId)
//     smartoperator  → default tenant
//
// BOOTSTRAP PASSWORD
//   Default plaintext is "Change@123" with MustChangePassword=true. Override
//   via Seed:DefaultUserPassword in user-secrets so even the bootstrap value
//   isn't checked in.
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Application.Abstractions.Auth;
using Platform.Application.Abstractions.Seeding;
using Platform.Application.Configuration;
using Platform.Application.Persistence;
using Platform.Domain.Identity;

namespace Platform.Infrastructure.Seeding.Defaults;

public sealed class UsersSeed : ISeedContributor
{
    public int Order => 30;

    public static readonly IReadOnlyList<UserDefinition> Defaults = new[]
    {
        // CrossTenant = true → ClientId left null (SystemAdmin scope).
        new UserDefinition("sysadmin",      "System Admin",   "sysadmin@smartcanteen.local",  "Admin",    new[] { "SystemAdmin" }, CrossTenant: true),
        new UserDefinition("smartadmin",    "Tenant Admin",   "admin@smartcanteen.local",     "Admin",    new[] { "TenantAdmin" }, CrossTenant: false),
        new UserDefinition("smartoperator", "Main Operator",  "operator@smartcanteen.local",  "Operator", new[] { "Operator" },    CrossTenant: false),
    };

    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly SeedOptions _options;
    private readonly TenancyOptions _tenancy;
    private readonly ILogger<UsersSeed> _logger;

    public UsersSeed(
        IAppDbContext db,
        IPasswordHasher hasher,
        IOptions<SeedOptions> options,
        IOptions<TenancyOptions> tenancy,
        ILogger<UsersSeed> logger)
    {
        _db = db;
        _hasher = hasher;
        _options = options.Value;
        _tenancy = tenancy.Value;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var users = _db.Set<User>();
        var roles = _db.Set<Role>();
        var userRoles = _db.Set<UserRole>();

        var defaultPassword = string.IsNullOrWhiteSpace(_options.DefaultUserPassword)
            ? "Change@123"
            : _options.DefaultUserPassword;

        var inserted = 0;
        foreach (var def in Defaults)
        {
            var user = await users.FirstOrDefaultAsync(u => u.UserName == def.UserName, cancellationToken);
            if (user is null)
            {
                user = new User
                {
                    UserName     = def.UserName,
                    DisplayName  = def.Display,
                    Email        = def.Email,
                    UserKind     = def.UserKind,
                    ClientId     = def.CrossTenant ? null : _tenancy.DefaultClientId,
                    PasswordHash = _hasher.Hash(defaultPassword),
                    MustChangePassword = true,
                    IsActive     = true,
                    CreatedAtUtc = DateTime.UtcNow
                };
                users.Add(user);
                await _db.SaveChangesAsync(cancellationToken);
                inserted++;
            }

            foreach (var roleCode in def.RoleCodes)
            {
                var role = await roles.FirstOrDefaultAsync(r => r.RoleCode == roleCode, cancellationToken);
                if (role is null) continue;
                var linked = await userRoles.AnyAsync(ur => ur.UserId == user.UserId && ur.RoleId == role.RoleId, cancellationToken);
                if (!linked) userRoles.Add(new UserRole { UserId = user.UserId, RoleId = role.RoleId });
            }
        }
        await _db.SaveChangesAsync(cancellationToken);

        if (inserted > 0)
        {
            _logger.LogInformation("Seeded {Count} default user(s). Bootstrap password is in use — change immediately.", inserted);
        }
    }

    public sealed record UserDefinition(
        string UserName,
        string Display,
        string Email,
        string UserKind,
        IReadOnlyList<string> RoleCodes,
        bool CrossTenant);
}
