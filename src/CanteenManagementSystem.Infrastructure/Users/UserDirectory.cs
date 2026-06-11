// =============================================================================
// UserDirectory  (CanteenManagementSystem.Infrastructure.Users)
// -----------------------------------------------------------------------------
// Single-stop lookup for the counter hot path. Returns a UserProfileSnapshot
// for whatever identifier the operator scanned/typed.
//
// CANTEEN-OWNED SCHEMA (SmartCanteen DB)
//   Reads ONLY from Students / Employees. These tables are populated by:
//     * Excel/CSV bulk upload (`/admin/users/bulk-import` + tenant directory UI).
//     * UI per-tenant creation.
//     * DirectorySyncService when a tenant configures an external source
//       (future "client integration" track).
//   No external DB views are read from this service — the bridge to legacy
//   school portals has been retired.
//
// ADR 0004: IReadOnlyRepository<T> — pure read.
// =============================================================================

using Platform.Application.Persistence;
using Platform.Application.Abstractions.Users;
using CanteenManagementSystem.Application.Tenancy;
using CanteenManagementSystem.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace CanteenManagementSystem.Infrastructure.Users;

internal sealed class UserDirectory : IUserDirectory
{
    private readonly IReadOnlyRepository<Student> _students;
    private readonly IReadOnlyRepository<Employee> _employees;
    private readonly IClientCacheService _clientCache;

    public UserDirectory(
        IReadOnlyRepository<Student> students,
        IReadOnlyRepository<Employee> employees,
        IClientCacheService clientCache)
    {
        _students = students;
        _employees = employees;
        _clientCache = clientCache;
    }

    public async Task<UserProfileSnapshot?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken = default)
    {
        identifier = identifier?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(identifier)) return null;

        var student = await _students.NoTrackingQuery()
            .Where(s => s.ExternalId == identifier || s.CardIdentifier == identifier)
            .FirstOrDefaultAsync(cancellationToken);
        if (student is not null) return MapStudent(student);

        var employee = await _employees.NoTrackingQuery()
            .Where(e => e.ExternalId == identifier || e.CardIdentifier == identifier)
            .FirstOrDefaultAsync(cancellationToken);
        if (employee is not null) return MapEmployee(employee);

        return null;
    }

    public async Task<IReadOnlyDictionary<(string UserId, string UserType), UserProfileSnapshot>> BatchLookupAsync(
        IEnumerable<(string UserId, string UserType)> keys,
        CancellationToken cancellationToken = default)
    {
        var keyList = keys.ToList();
        var result = new Dictionary<(string, string), UserProfileSnapshot>();
        if (keyList.Count == 0) return result;

        var studentIds  = keyList.Where(k => k.UserType == "Student").Select(k => k.UserId).Distinct().ToList();
        var employeeIds = keyList.Where(k => k.UserType == "Employee").Select(k => k.UserId).Distinct().ToList();

        if (studentIds.Count > 0)
        {
            var rows = await _students.NoTrackingQuery()
                .Where(s => studentIds.Contains(s.ExternalId))
                .ToListAsync(cancellationToken);
            foreach (var s in rows) result[(s.ExternalId, "Student")] = MapStudent(s);
        }
        if (employeeIds.Count > 0)
        {
            var rows = await _employees.NoTrackingQuery()
                .Where(e => employeeIds.Contains(e.ExternalId))
                .ToListAsync(cancellationToken);
            foreach (var e in rows) result[(e.ExternalId, "Employee")] = MapEmployee(e);
        }

        return result;
    }

    // ─── Mappers ──────────────────────────────────────────────────────────

    private UserProfileSnapshot MapStudent(Student s) => new(
        UserId: s.ExternalId,
        UserIdentifier: s.CardIdentifier ?? s.ExternalId,
        UserType: "Student",
        UserName: s.Name,
        PhotoUrl: _clientCache.GetPhotoUrl(s.PhotoPath),
        MobileNo: s.ContactNo ?? string.Empty,
        Gender: s.Gender ?? string.Empty,
        AcademicInformation: $"Program: {s.Program ?? "N/A"}; Version: {s.Version ?? "N/A"}; Session: {s.Session ?? "N/A"}; Section: {s.Section ?? "N/A"}",
        EmployeeTypeName: string.Empty);

    private UserProfileSnapshot MapEmployee(Employee e) => new(
        UserId: e.ExternalId,
        UserIdentifier: e.CardIdentifier ?? e.ExternalId,
        UserType: "Employee",
        UserName: e.Name,
        PhotoUrl: _clientCache.GetPhotoUrl(e.PhotoPath),
        MobileNo: e.ContactNo ?? string.Empty,
        Gender: e.Gender ?? string.Empty,
        AcademicInformation: $"Designation: {e.Designation ?? "N/A"}; Type: {e.EmployeeType ?? "N/A"}",
        EmployeeTypeName: e.EmployeeType ?? string.Empty);
}
