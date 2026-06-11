// =============================================================================
// MigrationOptions  (Platform.Application.Configuration)
// -----------------------------------------------------------------------------
// Bound from the top-level "Migration" key in appsettings.json. Kept SEPARATE
// from SeedOptions because migrations and seeding are independent decisions:
//
//   * Dev   : Migration.ApplyOnStartup=true  + Seed.RunOnStartup=true
//   * Prod  : Migration.ApplyOnStartup=false + Seed.RunOnStartup=true
//             (migrations run via CI/CD before pods boot; seed remains
//              idempotent and safe to run every restart)
//   * CI    : both false (the build container shouldn't touch the DB)
//
// USAGE
//   "Migration": {
//     "ApplyOnStartup": true,
//     "RetryCount":     3,
//     "RetryDelaySeconds": 5
//   }
// =============================================================================

namespace Platform.Application.Configuration;

public class MigrationOptions
{
    /// <summary>
    /// When true, the app calls Database.MigrateAsync() at startup. Set false
    /// in production where the release pipeline runs migrations explicitly.
    /// </summary>
    public bool ApplyOnStartup { get; set; } = true;

    /// <summary>How many times to retry MigrateAsync if it throws (transient SQL errors). 0 = no retry.</summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>Seconds to wait between migration retries.</summary>
    public int RetryDelaySeconds { get; set; } = 5;
}
