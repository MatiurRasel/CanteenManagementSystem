// =============================================================================
// IDatabaseSeeder  (Platform.Application.Abstractions.Seeding)
// -----------------------------------------------------------------------------
// Idempotent startup seeding. Runs:
//   1. EF migrations (if SeedOptions.ApplyMigrations = true).
//   2. Insert tenants / permissions / roles / users / gateways / API keys
//      from the Seed section of appsettings.json — but only if a row with the
//      same business key doesn't already exist (re-running is a no-op).
//
// EXTENSION POINT
//   Products can register additional ISeedContributor implementations to
//   seed their own canteen-specific reference data (e.g. default menu items).
// =============================================================================

namespace Platform.Application.Abstractions.Seeding;

public interface IDatabaseSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}

public interface ISeedContributor
{
    int Order { get; }
    Task SeedAsync(CancellationToken cancellationToken = default);
}
