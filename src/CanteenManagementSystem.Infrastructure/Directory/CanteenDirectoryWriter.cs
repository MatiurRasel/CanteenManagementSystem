// =============================================================================
// CanteenDirectoryWriter  (CanteenManagementSystem.Infrastructure.Directory)
// -----------------------------------------------------------------------------
// Canteen-specific IDirectoryWriter — maps DirectoryStudent/DirectoryEmployee
// DTOs onto the canteen-owned Students / Employees entities.
//
// IDEMPOTENT UPSERT
//   For each incoming row:
//     hash = SHA-256(normalised string concat)
//     local = SELECT WHERE ExternalId = dto.ExternalId  (auto tenant-filtered)
//     if local == null    -> INSERT (and stamp ClientId via SaveChanges)
//     if local.SourceHash == hash -> no-op
//     else                -> UPDATE (write fields + new hash)
//
// SOFT DELETE
//   Only when delta.IsFullSnapshot is true. We never hard-delete — orders /
//   wallets reference these rows. Disabled rows stay queryable but won't pass
//   the IsActive=true filter in counter scans.
//
// ADR 0004: IUnitOfWork + IRepository<T>.
// =============================================================================

using System.Security.Cryptography;
using System.Text;
using CanteenManagementSystem.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Application.Abstractions.Directory;
using Platform.Application.Persistence;

namespace CanteenManagementSystem.Infrastructure.Directory;

public sealed class CanteenDirectoryWriter : IDirectoryWriter
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<CanteenDirectoryWriter> _logger;

    public CanteenDirectoryWriter(IUnitOfWork uow, ILogger<CanteenDirectoryWriter> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<DirectoryWriteResult> WriteAsync(DirectoryDelta delta, CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var (sa, su, sd) = await UpsertStudentsAsync(delta.Students, delta.IsFullSnapshot, nowUtc, cancellationToken);
        var (ea, eu, ed) = await UpsertEmployeesAsync(delta.Employees, delta.IsFullSnapshot, nowUtc, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return new DirectoryWriteResult(sa, su, sd, ea, eu, ed);
    }

    private async Task<(int added, int updated, int disabled)> UpsertStudentsAsync(
        IReadOnlyList<DirectoryStudent> incoming, bool isFullSnapshot, DateTime nowUtc, CancellationToken ct)
    {
        var added = 0; var updated = 0; var disabled = 0;
        var students = _uow.Repository<Student>();

        foreach (var dto in incoming)
        {
            var hash = HashStudent(dto);
            var local = await students.FirstOrDefaultAsync(s => s.ExternalId == dto.ExternalId, ct);

            if (local is null)
            {
                await students.AddAsync(new Student
                {
                    ExternalId     = dto.ExternalId,
                    CardIdentifier = dto.CardIdentifier,
                    Name           = dto.Name,
                    Gender         = dto.Gender,
                    ContactNo      = dto.ContactNo,
                    PhotoPath      = dto.PhotoPath,
                    Program        = dto.Program,
                    Class          = dto.Class,
                    Section        = dto.Section,
                    Session        = dto.Session,
                    Version        = dto.Version,
                    IsActive       = true,
                    SyncedAtUtc    = nowUtc,
                    SourceHash     = hash
                }, ct);
                added++;
            }
            else if (local.SourceHash != hash || !local.IsActive)
            {
                local.CardIdentifier = dto.CardIdentifier;
                local.Name           = dto.Name;
                local.Gender         = dto.Gender;
                local.ContactNo      = dto.ContactNo;
                local.PhotoPath      = dto.PhotoPath;
                local.Program        = dto.Program;
                local.Class          = dto.Class;
                local.Section        = dto.Section;
                local.Session        = dto.Session;
                local.Version        = dto.Version;
                local.IsActive       = true;
                local.SyncedAtUtc    = nowUtc;
                local.SourceHash     = hash;
                updated++;
            }
        }

        if (isFullSnapshot)
        {
            var ids = incoming.Select(s => s.ExternalId).ToHashSet(StringComparer.Ordinal);
            var stale = await students.Query().Where(s => s.IsActive && !ids.Contains(s.ExternalId)).ToListAsync(ct);
            foreach (var s in stale)
            {
                s.IsActive = false;
                s.SyncedAtUtc = nowUtc;
                disabled++;
            }
        }

        return (added, updated, disabled);
    }

    private async Task<(int added, int updated, int disabled)> UpsertEmployeesAsync(
        IReadOnlyList<DirectoryEmployee> incoming, bool isFullSnapshot, DateTime nowUtc, CancellationToken ct)
    {
        var added = 0; var updated = 0; var disabled = 0;
        var employees = _uow.Repository<Employee>();

        foreach (var dto in incoming)
        {
            var hash = HashEmployee(dto);
            var local = await employees.FirstOrDefaultAsync(e => e.ExternalId == dto.ExternalId, ct);

            if (local is null)
            {
                await employees.AddAsync(new Employee
                {
                    ExternalId     = dto.ExternalId,
                    CardIdentifier = dto.CardIdentifier,
                    Name           = dto.Name,
                    Gender         = dto.Gender,
                    ContactNo      = dto.ContactNo,
                    PhotoPath      = dto.PhotoPath,
                    Designation    = dto.Designation,
                    EmployeeType   = dto.EmployeeType,
                    IsActive       = true,
                    SyncedAtUtc    = nowUtc,
                    SourceHash     = hash
                }, ct);
                added++;
            }
            else if (local.SourceHash != hash || !local.IsActive)
            {
                local.CardIdentifier = dto.CardIdentifier;
                local.Name           = dto.Name;
                local.Gender         = dto.Gender;
                local.ContactNo      = dto.ContactNo;
                local.PhotoPath      = dto.PhotoPath;
                local.Designation    = dto.Designation;
                local.EmployeeType   = dto.EmployeeType;
                local.IsActive       = true;
                local.SyncedAtUtc    = nowUtc;
                local.SourceHash     = hash;
                updated++;
            }
        }

        if (isFullSnapshot)
        {
            var ids = incoming.Select(e => e.ExternalId).ToHashSet(StringComparer.Ordinal);
            var stale = await employees.Query().Where(e => e.IsActive && !ids.Contains(e.ExternalId)).ToListAsync(ct);
            foreach (var e in stale)
            {
                e.IsActive = false;
                e.SyncedAtUtc = nowUtc;
                disabled++;
            }
        }

        return (added, updated, disabled);
    }

    // Hashing strategy: pipe-delimited normalised field concat → SHA-256 hex.
    // Order MUST stay stable; changing the formula on disk would mark every row
    // as "updated" on the next sync. Treat this method like an SQL schema column.
    private static string HashStudent(DirectoryStudent s)
        => Sha256Hex($"{s.ExternalId}|{s.CardIdentifier}|{s.Name}|{s.Gender}|{s.ContactNo}|{s.PhotoPath}|{s.Program}|{s.Class}|{s.Section}|{s.Session}|{s.Version}");

    private static string HashEmployee(DirectoryEmployee e)
        => Sha256Hex($"{e.ExternalId}|{e.CardIdentifier}|{e.Name}|{e.Gender}|{e.ContactNo}|{e.PhotoPath}|{e.Designation}|{e.EmployeeType}");

    private static string Sha256Hex(string s)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes);
    }
}
