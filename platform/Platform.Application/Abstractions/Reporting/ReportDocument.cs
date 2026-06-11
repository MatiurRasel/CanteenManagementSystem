// =============================================================================
// ReportDocument  (Platform.Application.Abstractions.Reporting)
// -----------------------------------------------------------------------------
// Format-agnostic in-memory representation of a single report.
//
//   ReportDocument
//     ├── Title              "Daily collection"
//     ├── Subtitle           "2026-06-01 → 2026-06-30"
//     ├── GeneratedAtUtc     timestamp stamped on every page header
//     ├── TenantName         resolved from ITenantContext at run time
//     ├── Summary[]          one-line KPIs ("Total revenue: ৳ 12,340")
//     └── Sections[]         repeating tables OR free text blocks
//                              ├── Heading
//                              ├── Columns        (Csv/Xlsx/Pdf use this)
//                              └── Rows           (object[]; row[i] is a cell)
//
// EVERY renderer maps this shape into its format:
//   * CSV   → one section per file, columns become headers
//   * XLSX  → one section per worksheet
//   * PDF   → page header + flowed sections with table grids
//   * HTML  → <h1>title, <h2>section, <table>rows
//   * JSON  → straight serialise
//
// Reports stay DUMB about output — they just shape data.
// =============================================================================

namespace Platform.Application.Abstractions.Reporting;

public sealed class ReportDocument
{
    public string Title { get; init; } = string.Empty;
    public string? Subtitle { get; init; }
    public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;
    public string? TenantName { get; init; }
    public IReadOnlyList<ReportKpi> Summary { get; init; } = Array.Empty<ReportKpi>();
    public IReadOnlyList<ReportSection> Sections { get; init; } = Array.Empty<ReportSection>();
}

public sealed record ReportKpi(string Label, string Value, string? Hint = null);

public sealed class ReportSection
{
    public string Heading { get; init; } = string.Empty;
    public string? Description { get; init; }
    public IReadOnlyList<ReportColumn> Columns { get; init; } = Array.Empty<ReportColumn>();
    public IReadOnlyList<IReadOnlyList<object?>> Rows { get; init; } = Array.Empty<IReadOnlyList<object?>>();
    public string? FreeTextHtml { get; init; }       // if set, renderers print this instead of the table
    public bool TotalsRow { get; init; }              // if true, last row is bolded by renderers
}

public sealed record ReportColumn(
    string Header,
    ReportColumnType Type = ReportColumnType.Text,
    string? Format = null,
    int? WidthPercent = null,
    bool AlignRight = false);

public enum ReportColumnType
{
    Text    = 1,
    Number  = 2,
    Money   = 3,
    Percent = 4,
    Date    = 5,
    DateTime = 6,
    Boolean  = 7
}
