// =============================================================================
// ManualDirectorySource  (Platform.Infrastructure.Directory.Sources)
// -----------------------------------------------------------------------------
// No-op pull source — represents "tenants who upload CSVs from the admin UI".
// FetchAsync returns an empty delta so the background worker doesn't error
// when it touches a manual-mode tenant; the CSV upload endpoint goes through
// IDirectorySyncService.IngestAsync directly (bypassing the source path).
//
// WHY keep it instead of returning null?
//   The factory could return null and let DirectorySyncService skip. Choosing
//   to return this no-op source instead keeps the audit trail consistent: a
//   "Manual" tenant that's never uploaded still gets a no-op DirectorySyncRun
//   row when the worker ticks, so admins see "last sync 2 min ago: 0 records"
//   instead of a confusing gap.
// =============================================================================

using Platform.Application.Abstractions.Directory;

namespace Platform.Infrastructure.Directory.Sources;

public sealed class ManualDirectorySource : IDirectorySource
{
    public Task<DirectoryDelta> FetchAsync(DateTime sinceUtc, CancellationToken cancellationToken = default)
        => Task.FromResult(new DirectoryDelta(
            Students:         Array.Empty<DirectoryStudent>(),
            Employees:        Array.Empty<DirectoryEmployee>(),
            HighWatermarkUtc: DateTime.UtcNow,
            IsFullSnapshot:   false));
}
