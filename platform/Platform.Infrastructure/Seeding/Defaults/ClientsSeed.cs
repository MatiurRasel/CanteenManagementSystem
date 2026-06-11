// =============================================================================
// ClientsSeed  (Platform.Infrastructure.Seeding.Defaults)
// -----------------------------------------------------------------------------
// Order = 5. Runs FIRST — every other seed (users, gateways, canteen menu)
// gets ClientId stamped onto it, so the tenant row MUST exist before they run.
//
// THE BOOTSTRAP TENANT
//   Reads TenancyOptions.DefaultClientId / DefaultClientCode and writes one
//   row keyed by ClientCode. If the tenant already exists, the seed is a
//   no-op. If you change appsettings -> Tenancy:DefaultClientCode and restart,
//   a NEW row is created — old rows are left alone (no rename).
//
// MULTI-TENANT PROVISIONING
//   Onboarding additional tenants is NOT a seed concern — that's an admin
//   flow (POST /api/v1/admin/tenants). This seed just guarantees the
//   single bootstrap tenant exists so the system can boot.
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Application.Abstractions.Seeding;
using Platform.Application.Configuration;
using Platform.Application.Persistence;
using Platform.Domain.Tenancy;

namespace Platform.Infrastructure.Seeding.Defaults;

public sealed class ClientsSeed : ISeedContributor
{
    public int Order => 5;  // before everything else

    private readonly IAppDbContext _db;
    private readonly TenancyOptions _tenancy;
    private readonly ILogger<ClientsSeed> _logger;

    public ClientsSeed(IAppDbContext db, IOptions<TenancyOptions> tenancy, ILogger<ClientsSeed> logger)
    {
        _db = db;
        _tenancy = tenancy.Value;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var clients = _db.Set<Client>();
        var code = _tenancy.DefaultClientCode;
        var desiredId = _tenancy.DefaultClientId;

        if (await clients.AnyAsync(c => c.ClientCode == code, cancellationToken))
        {
            return;
        }

        // Clients.ClientId is an IDENTITY column. We want the bootstrap tenant to
        // land at exactly TenancyOptions.DefaultClientId so the "no-tenant-resolved"
        // fallback in TenantContextMiddleware points at this row. Toggle
        // IDENTITY_INSERT around the single INSERT (same pattern NopCommerce uses).
        // All three statements share one batch so the session-scoped SET applies.
        await _db.Database.ExecuteSqlRawAsync(
            @"SET IDENTITY_INSERT [Clients] ON;
              INSERT INTO [Clients] ([ClientId], [ClientCode], [ClientName], [ShortName], [IsActive], [IsolationMode], [CreatedAtUtc])
              VALUES ({0}, {1}, {2}, {3}, 1, 0, {4});
              SET IDENTITY_INSERT [Clients] OFF;",
            parameters: new object[] { desiredId, code, $"{code} (default tenant)", code, DateTime.UtcNow },
            cancellationToken);

        _logger.LogInformation("Seeded default tenant {ClientCode} (Id={ClientId}).", code, desiredId);
    }
}
