// =============================================================================
// PermissionsSeed  (Platform.Infrastructure.Seeding.Defaults)
// -----------------------------------------------------------------------------
// Order = 10. Inserted FIRST because every other seed depends on permissions
// existing in the DB.
//
// EXTENDING
//   Products (Canteen, Rent, Clinic) add their own permissions by writing
//   another ISeedContributor with Order > 10 and inserting product-specific
//   permission codes (e.g. "Lease.Renew", "Appointment.Book").
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Application.Abstractions.Seeding;
using Platform.Application.Persistence;
using Platform.Domain.Identity;

namespace Platform.Infrastructure.Seeding.Defaults;

public sealed class PermissionsSeed : ISeedContributor
{
    public int Order => 10;

    /// <summary>Canonical platform permissions. Add new codes by appending to this list.</summary>
    public static readonly IReadOnlyList<(string Code, string Display)> Defaults = new[]
    {
        ("Wallet.View",      "View wallet balances"),
        ("Wallet.Recharge",  "Recharge a wallet"),
        ("Wallet.Manage",    "Manage wallets"),
        ("Order.Place",      "Place an order"),
        ("Order.Deliver",    "Mark order delivered"),
        ("Order.View",       "View orders"),
        ("Menu.Manage",      "Manage menus"),
        ("Card.Read",        "Read NFC cards"),
        ("Card.Manage",      "Manage NFC card lifecycle"),
        ("Verification.Run", "Run identity verification"),
        ("Reports.View",     "View reports"),
        ("Audit.View",       "View audit trail"),
        ("Gateway.Manage",   "Manage payment gateways"),
        ("ApiKey.Manage",    "Issue and revoke service-to-service API keys"),
        ("User.Manage",      "Manage users + roles"),
    };

    private readonly IAppDbContext _db;
    private readonly ILogger<PermissionsSeed> _logger;

    public PermissionsSeed(IAppDbContext db, ILogger<PermissionsSeed> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var set = _db.Set<Permission>();
        var inserted = 0;
        foreach (var (code, display) in Defaults)
        {
            if (await set.AnyAsync(x => x.PermissionCode == code, cancellationToken)) continue;
            set.Add(new Permission { PermissionCode = code, DisplayName = display });
            inserted++;
        }
        if (inserted > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded {Count} permission(s).", inserted);
        }
    }
}
