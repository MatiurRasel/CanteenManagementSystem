// =============================================================================
// DirectoryAdminQueryService  (CanteenManagementSystem.Infrastructure.Directory)
// -----------------------------------------------------------------------------
// IDirectoryAdminQueryService impl. Repos only — no IAppDbContext. Tenant scope
// is provided by the global query filter, so callers see only their own rows.
// =============================================================================

using CanteenManagementSystem.Application.Directory;
using CanteenManagementSystem.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions.Directory;
using Platform.Application.Persistence;
using Platform.Domain.Directory;

namespace CanteenManagementSystem.Infrastructure.Directory;

internal sealed class DirectoryAdminQueryService : IDirectoryAdminQueryService
{
    private readonly IReadOnlyRepository<DirectorySyncRun> _runs;
    private readonly IReadOnlyRepository<Student> _students;
    private readonly IReadOnlyRepository<Employee> _employees;

    public DirectoryAdminQueryService(
        IReadOnlyRepository<DirectorySyncRun> runs,
        IReadOnlyRepository<Student> students,
        IReadOnlyRepository<Employee> employees)
    {
        _runs = runs;
        _students = students;
        _employees = employees;
    }

    public async Task<DirectoryHealthSnapshot> GetHealthSnapshotAsync(CancellationToken ct = default)
    {
        var lastSuccess = await _runs.NoTrackingQuery()
            .Where(r => r.Status == "Success")
            .OrderByDescending(r => r.StartedAtUtc)
            .Select(r => (DateTime?)r.CompletedAtUtc)
            .FirstOrDefaultAsync(ct);

        var lastError = await _runs.NoTrackingQuery()
            .Where(r => r.Status == "Failed")
            .OrderByDescending(r => r.StartedAtUtc)
            .Select(r => r.ErrorMessage)
            .FirstOrDefaultAsync(ct);

        var studentsTotal  = await _students.CountAsync(s => s.IsActive, ct);
        var employeesTotal = await _employees.CountAsync(e => e.IsActive, ct);

        return new DirectoryHealthSnapshot(studentsTotal, employeesTotal, lastSuccess, lastError);
    }

    public async Task<IReadOnlyList<DirectorySyncRun>> GetRecentRunsAsync(int take, CancellationToken ct = default)
        => await _runs.NoTrackingQuery()
            .OrderByDescending(r => r.StartedAtUtc)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DirectorySyncRunSummary>> GetRecentRunSummariesAsync(int take, CancellationToken ct = default)
        => await _runs.NoTrackingQuery()
            .OrderByDescending(r => r.StartedAtUtc)
            .Take(take)
            .Select(r => new DirectorySyncRunSummary(
                r.SyncRunId, r.Status, r.Source, r.StartedAtUtc, r.CompletedAtUtc,
                r.StudentsAdded, r.StudentsUpdated, r.StudentsDisabled,
                r.EmployeesAdded, r.EmployeesUpdated, r.EmployeesDisabled,
                r.ErrorMessage))
            .ToListAsync(ct);

    public async Task<PagedRoster<Student>> SearchStudentsAsync(int page, int pageSize, string? q, CancellationToken ct = default)
    {
        var qry = _students.NoTrackingQuery().Where(s => s.IsActive);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var needle = q.Trim();
            qry = qry.Where(s => s.Name.Contains(needle) || s.ExternalId.Contains(needle)
                              || (s.CardIdentifier != null && s.CardIdentifier.Contains(needle)));
        }
        var total = await qry.CountAsync(ct);
        var rows = await qry.OrderBy(s => s.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedRoster<Student>(rows, page, pageSize, total, q);
    }

    public async Task<PagedRoster<Employee>> SearchEmployeesAsync(int page, int pageSize, string? q, CancellationToken ct = default)
    {
        var qry = _employees.NoTrackingQuery().Where(e => e.IsActive);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var needle = q.Trim();
            qry = qry.Where(e => e.Name.Contains(needle) || e.ExternalId.Contains(needle)
                              || (e.CardIdentifier != null && e.CardIdentifier.Contains(needle)));
        }
        var total = await qry.CountAsync(ct);
        var rows = await qry.OrderBy(e => e.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedRoster<Employee>(rows, page, pageSize, total, q);
    }

    public async Task<IReadOnlyList<Student>> ListAllActiveStudentsAsync(CancellationToken ct = default)
        => await _students.NoTrackingQuery().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<Employee>> ListAllActiveEmployeesAsync(CancellationToken ct = default)
        => await _employees.NoTrackingQuery().Where(e => e.IsActive).OrderBy(e => e.Name).ToListAsync(ct);
}
