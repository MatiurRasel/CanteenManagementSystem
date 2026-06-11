// =============================================================================
// RolesSeed  (Platform.Infrastructure.Seeding.Defaults)
// -----------------------------------------------------------------------------
// Order = 20. Runs after PermissionsSeed (Order=10).
//
// PROPERTIES OF THE DEFAULT ROLE GRAPH
//   SystemAdmin -> EVERY permission (the wildcard "*")
//   TenantAdmin -> management surface, no system-wide ops
//   Operator    -> day-to-day counter actions
//   Cashier     -> minimal counter actions
//   Auditor     -> read-only across reports + audit
//   Parent      -> read own children
//   Student     -> place + view own orders
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Application.Abstractions.Seeding;
using Platform.Application.Persistence;
using Platform.Domain.Identity;

namespace Platform.Infrastructure.Seeding.Defaults;

public sealed class RolesSeed : ISeedContributor
{
    public int Order => 20;

    public static readonly IReadOnlyList<RoleDefinition> Defaults = new[]
    {
        new RoleDefinition("SystemAdmin", "System Admin", "Full system control across every tenant.", true,
            new[] { "*" }),
        new RoleDefinition("TenantAdmin", "Tenant Admin", "Owns one tenant's configuration + users.", false,
            new[] { "Wallet.Manage", "Wallet.View", "Order.View", "Menu.Manage", "Card.Manage",
                    "Reports.View", "Audit.View", "Gateway.Manage", "ApiKey.Manage", "User.Manage" }),
        new RoleDefinition("Operator", "Counter Operator", "Runs the canteen counter day-to-day.", false,
            new[] { "Wallet.View", "Order.Place", "Order.Deliver", "Order.View", "Card.Read", "Verification.Run" }),
        new RoleDefinition("Cashier", "Cashier", "Minimal counter access.", false,
            new[] { "Order.Place", "Order.Deliver", "Wallet.View" }),
        new RoleDefinition("Auditor", "Auditor", "Read-only across reports + audit.", false,
            new[] { "Reports.View", "Audit.View", "Order.View" }),
        new RoleDefinition("Parent", "Parent", "Monitors child accounts.", false,
            new[] { "Wallet.View", "Order.View" }),
        new RoleDefinition("Student", "Student", "End-user student/staff/employee.", false,
            new[] { "Wallet.View", "Order.Place" }),
        // Synthetic role granted to paired kiosk tablets via the device-token
        // cookie. Never assigned to a real human user. See KioskController.Pair.
        new RoleDefinition("Kiosk", "Kiosk device", "Self-service tablet — scoped to the verification + order-place surface only.", true,
            new[] { "Order.Place", "Verification.Run", "Wallet.View", "Card.Read" }),
    };

    private readonly IAppDbContext _db;
    private readonly ILogger<RolesSeed> _logger;

    public RolesSeed(IAppDbContext db, ILogger<RolesSeed> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var roles = _db.Set<Role>();
        var perms = _db.Set<Permission>();
        var rolePerms = _db.Set<RolePermission>();

        var allPerms = await perms.ToListAsync(cancellationToken);
        var rolesInserted = 0;
        var linksInserted = 0;

        foreach (var def in Defaults)
        {
            var role = await roles.FirstOrDefaultAsync(r => r.RoleCode == def.Code, cancellationToken);
            if (role is null)
            {
                role = new Role
                {
                    RoleCode = def.Code,
                    DisplayName = def.Display,
                    Description = def.Description,
                    IsSystem = def.IsSystem
                };
                roles.Add(role);
                await _db.SaveChangesAsync(cancellationToken);
                rolesInserted++;
            }

            var targetPerms = def.Permissions.Contains("*")
                ? allPerms
                : allPerms.Where(p => def.Permissions.Contains(p.PermissionCode)).ToList();

            foreach (var perm in targetPerms)
            {
                var exists = await rolePerms.AnyAsync(rp => rp.RoleId == role.RoleId && rp.PermissionId == perm.PermissionId, cancellationToken);
                if (exists) continue;
                rolePerms.Add(new RolePermission { RoleId = role.RoleId, PermissionId = perm.PermissionId });
                linksInserted++;
            }
        }

        if (rolesInserted > 0 || linksInserted > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded {Roles} role(s) and {Links} role-permission link(s).", rolesInserted, linksInserted);
        }
    }

    public sealed record RoleDefinition(
        string Code,
        string Display,
        string Description,
        bool IsSystem,
        IReadOnlyList<string> Permissions);
}
