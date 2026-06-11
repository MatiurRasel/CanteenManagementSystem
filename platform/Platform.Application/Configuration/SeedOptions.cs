// =============================================================================
// SeedOptions  (Platform.Application.Configuration)
// -----------------------------------------------------------------------------
// Bound from the top-level "Seed" key in appsettings.json. SEPARATE from
// MigrationOptions — migration on/off and seeding on/off are independent.
//
// AppSettings-bound TOGGLES ONLY. The seed *data* lives in C# classes
// (one per concern) implementing ISeedContributor — never in appsettings.
//
// USAGE
//   "Seed": {
//     "RunOnStartup":         true,
//     "DefaultUserPassword":  ""        // set via dotnet user-secrets
//   }
//
// WHY data-in-code, toggles-in-config
//   * Refactor-safe — seed rows benefit from compiler / IntelliSense.
//   * Strongly-typed — no JSON ↔ POCO drift.
//   * Reviewable — seed changes ride PRs alongside the schema change.
//   * Operators can still flip ON / OFF per environment without redeploy.
// =============================================================================

namespace Platform.Application.Configuration;

public class SeedOptions
{
    /// <summary>Master switch. When false, the seeder skips all contributors.</summary>
    public bool RunOnStartup { get; set; } = true;

    /// <summary>
    /// Optional override for the default seed users' password. Empty -> falls back to
    /// "Change@123" with MustChangePassword=true. Recommended to set via dotnet
    /// user-secrets so the bootstrap password is never checked in.
    /// </summary>
    public string? DefaultUserPassword { get; set; }
}
