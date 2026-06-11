// =============================================================================
// DatabaseSeeder  (Platform.Infrastructure.Seeding)
// -----------------------------------------------------------------------------
// Pure orchestrator. AppSettings carries TWO SEPARATE keys of toggles:
//
//   "Migration": {
//     "ApplyOnStartup":    true,
//     "RetryCount":        3,
//     "RetryDelaySeconds": 5
//   },
//   "Seed": {
//     "RunOnStartup":        true,
//     "DefaultUserPassword": ""    // override bootstrap password
//   }
//
// Seed CONTENT lives in C# classes that implement ISeedContributor. Each one
// declares its Order (sort priority) and an idempotent SeedAsync. The seeder
// resolves every ISeedContributor from DI, sorts by Order ASC, and runs them
// sequentially.
//
// FLOW
//   1. If Migration.ApplyOnStartup: db.Database.MigrateAsync() (with retry).
//   2. If Seed.RunOnStartup:        foreach contributor: SeedAsync().
//
// ERROR HANDLING
//   Migration failure after exhausting retries is logged but doesn't crash
//   startup — operators can apply migrations out-of-band via
//   `dotnet ef database update`. Contributor failures DO bubble (we don't
//   want a half-seeded DB).
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Application.Abstractions.Seeding;
using Platform.Application.Configuration;
using Platform.Application.Persistence;

namespace Platform.Infrastructure.Seeding;

public sealed class DatabaseSeeder : IDatabaseSeeder
{
    private readonly IServiceProvider _services;
    private readonly MigrationOptions _migrationOptions;
    private readonly SeedOptions _seedOptions;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        IServiceProvider services,
        IOptions<MigrationOptions> migrationOptions,
        IOptions<SeedOptions> seedOptions,
        ILogger<DatabaseSeeder> logger)
    {
        _services = services;
        _migrationOptions = migrationOptions.Value;
        _seedOptions = seedOptions.Value;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _services.CreateScope();
        var sp = scope.ServiceProvider;

        // --- Step 1: apply migrations -------------------------------------
        if (_migrationOptions.ApplyOnStartup)
        {
            await TryMigrateAsync(sp, cancellationToken);
        }
        else
        {
            _logger.LogInformation("Migration:ApplyOnStartup=false — skipping migration step.");
        }

        // --- Step 2: data seed --------------------------------------------
        if (!_seedOptions.RunOnStartup)
        {
            _logger.LogInformation("Seed:RunOnStartup=false — skipping data seeding.");
            return;
        }

        var contributors = sp.GetServices<ISeedContributor>().OrderBy(c => c.Order).ToList();
        _logger.LogInformation("Running {Count} seed contributor(s).", contributors.Count);

        foreach (var contrib in contributors)
        {
            try
            {
                await contrib.SeedAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Seed contributor {Type} (Order={Order}) failed.", contrib.GetType().Name, contrib.Order);
                throw;  // half-seeded DB is worse than no seed
            }
        }

        _logger.LogInformation("Database seeding complete.");
    }

    private async Task TryMigrateAsync(IServiceProvider sp, CancellationToken cancellationToken)
    {
        var db = sp.GetRequiredService<IAppDbContext>();
        if (db is not DbContext concrete)
        {
            _logger.LogWarning("IAppDbContext is not a DbContext — skipping migration.");
            return;
        }

        var attempts = Math.Max(1, _migrationOptions.RetryCount + 1);
        var delay = TimeSpan.FromSeconds(Math.Max(0, _migrationOptions.RetryDelaySeconds));

        for (var i = 1; i <= attempts; i++)
        {
            try
            {
                await concrete.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("Database migrations applied (attempt {Attempt}/{Total}).", i, attempts);
                return;
            }
            catch (Exception ex) when (i < attempts)
            {
                _logger.LogWarning(ex, "Migration attempt {Attempt}/{Total} failed; retrying in {Delay}s.", i, attempts, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Migration failed after {Total} attempt(s). The app will continue to boot — " +
                    "resolve manually with `dotnet ef database update`. Turn Migration:ApplyOnStartup=false " +
                    "in production where migrations run via CI/CD.", attempts);
            }
        }
    }
}
