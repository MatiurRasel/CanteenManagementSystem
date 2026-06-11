// =============================================================================
// IDirectorySyncService  (Platform.Application.Abstractions.Directory)
// -----------------------------------------------------------------------------
// Orchestrator used by background sync, admin "Sync now", and CSV upload.
// All three converge on this single seam so the audit trail
// (DirectorySyncRun rows) is consistent regardless of trigger.
//
// THREE ENTRY POINTS — same engine
//   * IngestAsync     — caller already has the delta (CSV upload path)
//   * SyncTenantAsync — caller passes a tenant id; service resolves the
//                       configured source via IDirectorySourceFactory and pulls
//   * SyncAllDueAsync — background loop: runs SyncTenantAsync for every active
//                       tenant past its scheduled interval, returns summaries
// =============================================================================

namespace Platform.Application.Abstractions.Directory;

public interface IDirectorySyncService
{
    /// <summary>
    /// Persist a pre-built delta (CSV upload, webhook push). Bypasses source
    /// resolution; the caller supplies the bytes.
    /// </summary>
    Task<DirectorySyncRunSummary> IngestAsync(
        DirectoryDelta delta,
        string sourceLabel,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve the configured source for the current tenant context and run it.
    /// Caller is responsible for entering a tenant-scoped DI scope first.
    /// Returns null when the source is "Manual" / "Disabled" / not configured.
    /// </summary>
    Task<DirectorySyncRunSummary?> SyncCurrentTenantAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Background-worker hook: enumerate all active tenants, enter a scope for
    /// each, run SyncCurrentTenantAsync if the tenant is past its interval.
    /// Honours circuit breaker (HealthStatus="Disabled" skips immediately).
    /// </summary>
    Task<IReadOnlyList<DirectorySyncRunSummary>> SyncAllDueAsync(CancellationToken cancellationToken = default);
}

/// Light projection of a DirectorySyncRun row.
public sealed record DirectorySyncRunSummary(
    long SyncRunId,
    string Status,
    string Source,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    int StudentsAdded,
    int StudentsUpdated,
    int StudentsDisabled,
    int EmployeesAdded,
    int EmployeesUpdated,
    int EmployeesDisabled,
    string? ErrorMessage);
