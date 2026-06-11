// =============================================================================
// Directory DTOs  (Platform.Application.Abstractions.Directory)
// -----------------------------------------------------------------------------
// Source-system-agnostic shape of student/employee data. Every IDirectorySource
// (DatabaseDirectorySource, ApiDirectorySource, ManualDirectorySource) emits
// these records; every IDirectoryWriter consumes them. Products map them onto
// their own entities (Canteen → Students/Employees; Rent could map them to
// Tenants/Residents).
//
// WHY DTOs (not the entities themselves)
//   The sync runtime lives in Platform, but Student / Employee are canteen
//   domain entities. Crossing the layer with concrete entities would break the
//   product-extension story for Rent / Clinic. DTOs keep the runtime generic.
// =============================================================================

namespace Platform.Application.Abstractions.Directory;

/// One delta batch from a directory source — students + employees + watermark.
public sealed record DirectoryDelta(
    IReadOnlyList<DirectoryStudent> Students,
    IReadOnlyList<DirectoryEmployee> Employees,
    DateTime HighWatermarkUtc,
    /// <summary>
    /// When true, the writer treats this as the COMPLETE current set of users —
    /// rows missing from the delta get soft-disabled. When false (incremental),
    /// only adds/updates apply; missing rows are left alone.
    /// </summary>
    bool IsFullSnapshot);

public sealed record DirectoryStudent(
    string ExternalId,
    string Name,
    string? CardIdentifier = null,
    string? Gender = null,
    string? ContactNo = null,
    string? PhotoPath = null,
    string? Program = null,
    string? Class = null,
    string? Section = null,
    string? Session = null,
    string? Version = null);

public sealed record DirectoryEmployee(
    string ExternalId,
    string Name,
    string? CardIdentifier = null,
    string? Gender = null,
    string? ContactNo = null,
    string? PhotoPath = null,
    string? Designation = null,
    string? EmployeeType = null);

/// Counts returned from IDirectoryWriter.WriteAsync. Sync service copies these
/// onto the DirectorySyncRun audit row.
public sealed record DirectoryWriteResult(
    int StudentsAdded,
    int StudentsUpdated,
    int StudentsDisabled,
    int EmployeesAdded,
    int EmployeesUpdated,
    int EmployeesDisabled);
