// =============================================================================
// DatabaseDirectorySource  (Platform.Infrastructure.Directory.Sources)
// -----------------------------------------------------------------------------
// Reads the tenant's school SIS DB views directly via ADO.NET. NOT EF — the
// connection string is per-tenant + read-only; spinning up a DbContext per
// tenant just to project two views isn't worth it.
//
// CONNECTION STRING
//   Pulled from CanteenTenantSettings.Directory.Database.ConnectionString
//   (DataProtection-encrypted at rest). Connection lifetime = one FetchAsync;
//   we don't pool per-tenant.
//
// VIEW NAMES
//   Default to vw_StudentInfo_Canteen / vw_EmployeeInfo_Canteen but each
//   tenant can override (the school portal team may name views differently).
//
// SNAPSHOT MODE
//   Always emits a FULL SNAPSHOT — these views don't expose a "last modified"
//   column we can use for delta. The writer's hash-compare absorbs the cost
//   of N rows when only K change.
// =============================================================================

using Microsoft.Data.SqlClient;
using Platform.Application.Abstractions.Directory;

namespace Platform.Infrastructure.Directory.Sources;

public sealed class DatabaseDirectorySource : IDirectorySource
{
    private readonly string _connectionString;
    private readonly string _studentsView;
    private readonly string _employeesView;

    public DatabaseDirectorySource(string connectionString, string studentsView, string employeesView)
    {
        _connectionString = connectionString;
        _studentsView     = SanitiseIdentifier(studentsView);
        _employeesView    = SanitiseIdentifier(employeesView);
    }

    public async Task<DirectoryDelta> FetchAsync(DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        var students  = await ReadStudentsAsync(conn, cancellationToken);
        var employees = await ReadEmployeesAsync(conn, cancellationToken);

        return new DirectoryDelta(
            Students:         students,
            Employees:        employees,
            HighWatermarkUtc: DateTime.UtcNow,
            IsFullSnapshot:   true);
    }

    private async Task<List<DirectoryStudent>> ReadStudentsAsync(SqlConnection conn, CancellationToken ct)
    {
        var list = new List<DirectoryStudent>(2048);
        // NOTE: identifier is sanitised in the ctor; this is a controlled internal name, not user-driven SQL.
        var sql = $@"SELECT
                        StudentID, StudentIDC, StudentName, StudentSex, ContactNo, PhotoPathS,
                        ProgramName, SectionName, SessionName, VersionName
                     FROM [{_studentsView}]";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.CommandTimeout = 60;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var externalId = reader.GetString(0);
            var card       = reader.IsDBNull(1) ? null : reader.GetString(1);
            var name       = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            if (string.IsNullOrWhiteSpace(externalId) || string.IsNullOrWhiteSpace(name)) continue;

            list.Add(new DirectoryStudent(
                ExternalId:     externalId,
                Name:           name,
                CardIdentifier: card,
                Gender:         NormaliseGender(reader.IsDBNull(3) ? null : reader.GetString(3)),
                ContactNo:      reader.IsDBNull(4) ? null : reader.GetString(4),
                PhotoPath:      reader.IsDBNull(5) ? null : reader.GetString(5),
                Program:        reader.IsDBNull(6) ? null : reader.GetString(6),
                Class:          null,                                       // not in view; reserved for future
                Section:        reader.IsDBNull(7) ? null : reader.GetString(7),
                Session:        reader.IsDBNull(8) ? null : reader.GetString(8),
                Version:        reader.IsDBNull(9) ? null : reader.GetString(9)));
        }
        return list;
    }

    private async Task<List<DirectoryEmployee>> ReadEmployeesAsync(SqlConnection conn, CancellationToken ct)
    {
        var list = new List<DirectoryEmployee>(512);
        var sql = $@"SELECT
                        EmployeeID, EmployeeName, EmployeeGender, MobileNo,
                        EmployeePhotoPath, DesignationName, EmployeeTypeName
                     FROM [{_employeesView}]";
        await using var cmd = new SqlCommand(sql, conn);
        cmd.CommandTimeout = 60;

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var externalId = reader.GetString(0);
            var name       = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            if (string.IsNullOrWhiteSpace(externalId) || string.IsNullOrWhiteSpace(name)) continue;

            list.Add(new DirectoryEmployee(
                ExternalId:     externalId,
                Name:           name,
                CardIdentifier: null,                                       // not in view
                Gender:         reader.IsDBNull(2) ? null : reader.GetString(2),
                ContactNo:      reader.IsDBNull(3) ? null : reader.GetString(3),
                PhotoPath:      reader.IsDBNull(4) ? null : reader.GetString(4),
                Designation:    reader.IsDBNull(5) ? null : reader.GetString(5),
                EmployeeType:   reader.IsDBNull(6) ? null : reader.GetString(6)));
        }
        return list;
    }

    // Allow letters / digits / underscore / dot only. Defence-in-depth: even
    // though the value comes from tenant-admin-only settings, we still bracket
    // and validate to make SQL injection via view rename impossible.
    private static string SanitiseIdentifier(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "vw_StudentInfo_Canteen";
        foreach (var c in s)
        {
            if (!(char.IsLetterOrDigit(c) || c == '_' || c == '.'))
                throw new InvalidOperationException($"Invalid view identifier: '{s}'.");
        }
        return s;
    }

    private static string? NormaliseGender(string? s)
        => string.Equals(s, "M", StringComparison.OrdinalIgnoreCase) ? "Male"
        :  string.Equals(s, "F", StringComparison.OrdinalIgnoreCase) ? "Female"
        :  s;
}
