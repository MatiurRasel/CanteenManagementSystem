// =============================================================================
// DirectoryCsvParser  (CanteenManagementSystem.Infrastructure.Directory.Csv)
// -----------------------------------------------------------------------------
// Minimal CSV → DirectoryStudent / DirectoryEmployee parser. Supports:
//   * Header row, case-insensitive, any column order
//   * Quoted fields, embedded commas, embedded double-quotes ("" escape)
//   * Trailing whitespace / BOM tolerance
//
// EXPECTED HEADERS
//   Students:  ExternalId, Name, [CardIdentifier, Gender, ContactNo,
//                                 PhotoPath, Program, Class, Section,
//                                 Session, Version]
//   Employees: ExternalId, Name, [CardIdentifier, Gender, ContactNo,
//                                 PhotoPath, Designation, EmployeeType]
//
//   ExternalId + Name are required. Everything else is optional.
//
// DELIBERATELY NOT USING CsvHelper
//   The deps cost more than the ~80 lines below. If we hit edge cases
//   (alternative quote chars, Excel locale separators) we can swap to
//   CsvHelper without touching call sites.
// =============================================================================

using Platform.Application.Abstractions.Directory;

namespace CanteenManagementSystem.Infrastructure.Directory.Csv;

public static class DirectoryCsvParser
{
    public static IReadOnlyList<DirectoryStudent> ParseStudents(Stream stream)
    {
        var rows = ReadRows(stream);
        if (rows.Count == 0) return Array.Empty<DirectoryStudent>();

        var headers = NormaliseHeaders(rows[0]);
        RequireHeader(headers, "ExternalId");
        RequireHeader(headers, "Name");

        var result = new List<DirectoryStudent>(rows.Count - 1);
        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            var externalId = ValueOrEmpty(headers, row, "ExternalId");
            var name       = ValueOrEmpty(headers, row, "Name");
            if (string.IsNullOrWhiteSpace(externalId) || string.IsNullOrWhiteSpace(name)) continue;

            result.Add(new DirectoryStudent(
                ExternalId:     externalId,
                Name:           name,
                CardIdentifier: NullIfBlank(ValueOrEmpty(headers, row, "CardIdentifier")),
                Gender:         NullIfBlank(ValueOrEmpty(headers, row, "Gender")),
                ContactNo:      NullIfBlank(ValueOrEmpty(headers, row, "ContactNo")),
                PhotoPath:      NullIfBlank(ValueOrEmpty(headers, row, "PhotoPath")),
                Program:        NullIfBlank(ValueOrEmpty(headers, row, "Program")),
                Class:          NullIfBlank(ValueOrEmpty(headers, row, "Class")),
                Section:        NullIfBlank(ValueOrEmpty(headers, row, "Section")),
                Session:        NullIfBlank(ValueOrEmpty(headers, row, "Session")),
                Version:        NullIfBlank(ValueOrEmpty(headers, row, "Version"))));
        }
        return result;
    }

    public static IReadOnlyList<DirectoryEmployee> ParseEmployees(Stream stream)
    {
        var rows = ReadRows(stream);
        if (rows.Count == 0) return Array.Empty<DirectoryEmployee>();

        var headers = NormaliseHeaders(rows[0]);
        RequireHeader(headers, "ExternalId");
        RequireHeader(headers, "Name");

        var result = new List<DirectoryEmployee>(rows.Count - 1);
        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            var externalId = ValueOrEmpty(headers, row, "ExternalId");
            var name       = ValueOrEmpty(headers, row, "Name");
            if (string.IsNullOrWhiteSpace(externalId) || string.IsNullOrWhiteSpace(name)) continue;

            result.Add(new DirectoryEmployee(
                ExternalId:     externalId,
                Name:           name,
                CardIdentifier: NullIfBlank(ValueOrEmpty(headers, row, "CardIdentifier")),
                Gender:         NullIfBlank(ValueOrEmpty(headers, row, "Gender")),
                ContactNo:      NullIfBlank(ValueOrEmpty(headers, row, "ContactNo")),
                PhotoPath:      NullIfBlank(ValueOrEmpty(headers, row, "PhotoPath")),
                Designation:    NullIfBlank(ValueOrEmpty(headers, row, "Designation")),
                EmployeeType:   NullIfBlank(ValueOrEmpty(headers, row, "EmployeeType"))));
        }
        return result;
    }

    // ─── parser internals ──────────────────────────────────────────────────

    private static List<string[]> ReadRows(Stream stream)
    {
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        var content = reader.ReadToEnd();
        return ParseCsvText(content);
    }

    /// State-machine CSV reader. One row per call.
    private static List<string[]> ParseCsvText(string text)
    {
        var rows = new List<string[]>();
        var cur  = new List<string>();
        var field = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')   // escaped quote
                    {
                        field.Append('"'); i++;
                    }
                    else inQuotes = false;
                }
                else field.Append(c);
                continue;
            }

            switch (c)
            {
                case '"': inQuotes = true; break;
                case ',': cur.Add(field.ToString()); field.Clear(); break;
                case '\r': break;            // ignored; \n drives line break
                case '\n':
                    cur.Add(field.ToString()); field.Clear();
                    if (cur.Count > 0 && !(cur.Count == 1 && string.IsNullOrEmpty(cur[0])))
                        rows.Add(cur.ToArray());
                    cur.Clear();
                    break;
                default: field.Append(c); break;
            }
        }

        // flush final row
        if (field.Length > 0 || cur.Count > 0)
        {
            cur.Add(field.ToString());
            if (!(cur.Count == 1 && string.IsNullOrEmpty(cur[0]))) rows.Add(cur.ToArray());
        }
        return rows;
    }

    private static Dictionary<string, int> NormaliseHeaders(string[] headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headerRow.Length; i++) map[headerRow[i].Trim()] = i;
        return map;
    }

    private static void RequireHeader(Dictionary<string, int> headers, string name)
    {
        if (!headers.ContainsKey(name))
            throw new InvalidOperationException($"CSV is missing required header '{name}'.");
    }

    private static string ValueOrEmpty(Dictionary<string, int> headers, string[] row, string name)
        => headers.TryGetValue(name, out var idx) && idx < row.Length ? row[idx].Trim() : string.Empty;

    private static string? NullIfBlank(string s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
