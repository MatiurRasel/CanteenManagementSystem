// =============================================================================
// CsvReportRenderer  (Platform.Infrastructure.Reporting)
// -----------------------------------------------------------------------------
// Renders ReportDocument → UTF-8 CSV.
//
// SHAPE
//   When the document has ONE section: a flat CSV with headers + rows.
//   When it has MANY sections: each section is preceded by a comment line
//   ("# section heading") so Excel / accountants still read it intelligibly,
//   AND the file is wrapped in a BOM so Excel auto-detects UTF-8.
// =============================================================================

using System.Globalization;
using System.Text;
using Platform.Application.Abstractions.Reporting;

namespace Platform.Infrastructure.Reporting;

public sealed class CsvReportRenderer : IReportRenderer
{
    public ReportFormat Format => ReportFormat.Csv;

    public Task<RenderedReport> RenderAsync(ReportDocument document, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        sb.Append('﻿');  // UTF-8 BOM for Excel.

        // Document header as comments — Excel skips lines starting with '#'.
        sb.AppendLine($"# {document.Title}");
        if (!string.IsNullOrWhiteSpace(document.Subtitle)) sb.AppendLine($"# {document.Subtitle}");
        sb.AppendLine($"# Generated: {document.GeneratedAtUtc:u}");
        if (!string.IsNullOrWhiteSpace(document.TenantName)) sb.AppendLine($"# Tenant: {document.TenantName}");

        if (document.Summary.Count > 0)
        {
            sb.AppendLine();
            foreach (var k in document.Summary) sb.AppendLine($"# {k.Label}: {k.Value}");
        }

        foreach (var section in document.Sections)
        {
            sb.AppendLine();
            sb.AppendLine($"# {section.Heading}");
            if (!string.IsNullOrEmpty(section.Description)) sb.AppendLine($"# {section.Description}");

            if (section.Columns.Count == 0) continue;
            sb.AppendLine(string.Join(',', section.Columns.Select(c => Escape(c.Header))));
            foreach (var row in section.Rows)
            {
                sb.AppendLine(string.Join(',', row.Select((cell, idx) =>
                    Escape(FormatCell(cell, idx < section.Columns.Count ? section.Columns[idx] : null)))));
            }
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var safeTitle = SafeFilename(document.Title);
        return Task.FromResult(new RenderedReport(
            bytes,
            ReportFormat.Csv.MimeType(),
            $"{safeTitle}_{document.GeneratedAtUtc:yyyyMMdd-HHmm}.csv"));
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    internal static string FormatCell(object? cell, ReportColumn? col)
    {
        if (cell is null) return string.Empty;
        if (col is null) return cell.ToString() ?? string.Empty;
        return col.Type switch
        {
            ReportColumnType.Money    => cell is IFormattable f1 ? f1.ToString(col.Format ?? "N2", CultureInfo.InvariantCulture) : cell.ToString() ?? string.Empty,
            ReportColumnType.Number   => cell is IFormattable f2 ? f2.ToString(col.Format ?? "N0", CultureInfo.InvariantCulture) : cell.ToString() ?? string.Empty,
            ReportColumnType.Percent  => cell is IFormattable f3 ? f3.ToString(col.Format ?? "P2", CultureInfo.InvariantCulture) : cell.ToString() ?? string.Empty,
            ReportColumnType.Date     => cell is DateTime dt1 ? dt1.ToString(col.Format ?? "yyyy-MM-dd", CultureInfo.InvariantCulture) : cell.ToString() ?? string.Empty,
            ReportColumnType.DateTime => cell is DateTime dt2 ? dt2.ToString(col.Format ?? "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : cell.ToString() ?? string.Empty,
            ReportColumnType.Boolean  => cell is bool b ? (b ? "TRUE" : "FALSE") : cell.ToString() ?? string.Empty,
            _ => cell.ToString() ?? string.Empty
        };
    }

    internal static string SafeFilename(string title)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var clean = new string(title.Select(c => invalid.Contains(c) || c == ' ' ? '_' : c).ToArray());
        return string.IsNullOrEmpty(clean) ? "report" : clean.ToLowerInvariant();
    }
}
