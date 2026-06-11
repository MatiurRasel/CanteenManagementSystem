// =============================================================================
// DirectorySyncRun  (Platform.Domain.Directory)
// -----------------------------------------------------------------------------
// Audit row written for every sync attempt. The admin UI reads this to power
// the "Last 10 runs" table on the Directory Health screen.
//
// One row per (Tenant, attempt). Live runs have Status="Running" until the
// orchestrator updates them to "Success" / "Failed". The platform-level
// background job purges rows older than 90 days.
//
// WHY this lives in Platform (not Canteen)
//   The sync runtime is cross-cutting — every SaaS product on this platform
//   (canteen, rent, clinic) sources its primary directory the same way. The
//   row shape is identical; only the entities synced differ.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;

namespace Platform.Domain.Directory;

[Table("DirectorySyncRuns")]
public class DirectorySyncRun : ITenantOwned
{
    [Key]
    public long SyncRunId { get; set; }

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>"Running" → "Success" / "Failed" / "Cancelled".</summary>
    [Required, StringLength(20)]
    public string Status { get; set; } = "Running";

    /// <summary>"Database" / "Api" / "Manual" — copied from the source resolved at start.</summary>
    [Required, StringLength(20)]
    public string Source { get; set; } = string.Empty;

    public int StudentsAdded { get; set; }
    public int StudentsUpdated { get; set; }
    public int StudentsDisabled { get; set; }

    public int EmployeesAdded { get; set; }
    public int EmployeesUpdated { get; set; }
    public int EmployeesDisabled { get; set; }

    [StringLength(2000)]
    public string? ErrorMessage { get; set; }

    /// <summary>High-water-mark of the source rows fetched in this run. Stamped onto Directory.LastSyncAtUtc tenant setting on Success.</summary>
    public DateTime? HighWatermarkUtc { get; set; }

    [NotMapped]
    public TimeSpan? Duration => CompletedAtUtc - StartedAtUtc;
}
