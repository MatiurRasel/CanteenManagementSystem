// =============================================================================
// IDirectoryAdminQueryService  (CanteenManagementSystem.Application.Directory)
// -----------------------------------------------------------------------------
// Read-only queries for the tenant-admin directory dashboards (UI + API). Keeps
// DirectoryAdminController + DirectoryAdminUiController ADR-0004 compliant by
// removing their IAppDbContext dependency.
// =============================================================================

using CanteenManagementSystem.Domain.Users;
using Platform.Application.Abstractions.Directory;
using Platform.Domain.Directory;

namespace CanteenManagementSystem.Application.Directory;

public sealed record DirectoryHealthSnapshot(
    int StudentsTotal,
    int EmployeesTotal,
    DateTime? LastSuccessAtUtc,
    string? LastErrorMessage);

public sealed record PagedRoster<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, string? Query);

public interface IDirectoryAdminQueryService
{
    Task<DirectoryHealthSnapshot> GetHealthSnapshotAsync(CancellationToken ct = default);

    /// <summary>Most-recent sync runs as full entities (used by the UI views).</summary>
    Task<IReadOnlyList<DirectorySyncRun>> GetRecentRunsAsync(int take, CancellationToken ct = default);

    /// <summary>Most-recent sync runs projected to the wire summary (used by the JSON API).</summary>
    Task<IReadOnlyList<DirectorySyncRunSummary>> GetRecentRunSummariesAsync(int take, CancellationToken ct = default);

    Task<PagedRoster<Student>> SearchStudentsAsync(int page, int pageSize, string? q, CancellationToken ct = default);
    Task<PagedRoster<Employee>> SearchEmployeesAsync(int page, int pageSize, string? q, CancellationToken ct = default);

    Task<IReadOnlyList<Student>> ListAllActiveStudentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Employee>> ListAllActiveEmployeesAsync(CancellationToken ct = default);
}
