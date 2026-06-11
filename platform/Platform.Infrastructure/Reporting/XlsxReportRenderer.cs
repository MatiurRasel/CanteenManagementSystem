// =============================================================================
// XlsxReportRenderer  (Platform.Infrastructure.Reporting)
// -----------------------------------------------------------------------------
// Renders ReportDocument → Excel workbook via ClosedXML (MIT).
//   * First worksheet  = "Summary" (title + subtitle + KPI list)
//   * One worksheet per ReportSection
//   * Money / Number / Percent columns get native Excel formatting so users
//     can sum / pivot without re-typing.
//   * TotalsRow flag bolds the final row.
//
// SHEET NAMES are sanitised: Excel forbids 31-char limit + a few special
// chars. We replace illegals with "_" and trim.
// =============================================================================

using ClosedXML.Excel;
using Platform.Application.Abstractions.Reporting;

namespace Platform.Infrastructure.Reporting;

public sealed class XlsxReportRenderer : IReportRenderer
{
    public ReportFormat Format => ReportFormat.Xlsx;

    public Task<RenderedReport> RenderAsync(ReportDocument document, CancellationToken cancellationToken = default)
    {
        using var wb = new XLWorkbook();

        // ── Summary sheet ────────────────────────────────────────────────
        var summary = wb.Worksheets.Add("Summary");
        summary.Cell(1, 1).Value = document.Title;
        summary.Cell(1, 1).Style.Font.Bold = true;
        summary.Cell(1, 1).Style.Font.FontSize = 16;

        var row = 2;
        if (!string.IsNullOrWhiteSpace(document.Subtitle))
        {
            summary.Cell(row, 1).Value = document.Subtitle;
            summary.Cell(row, 1).Style.Font.FontColor = XLColor.DarkGray;
            row++;
        }
        summary.Cell(row, 1).Value = $"Generated: {document.GeneratedAtUtc:u}";
        summary.Cell(row, 1).Style.Font.FontColor = XLColor.DarkGray;
        row++;
        if (!string.IsNullOrWhiteSpace(document.TenantName))
        {
            summary.Cell(row, 1).Value = $"Tenant: {document.TenantName}";
            row++;
        }
        row++;

        foreach (var k in document.Summary)
        {
            summary.Cell(row, 1).Value = k.Label;
            summary.Cell(row, 1).Style.Font.Bold = true;
            summary.Cell(row, 2).Value = k.Value;
            if (!string.IsNullOrWhiteSpace(k.Hint))
            {
                summary.Cell(row, 3).Value = k.Hint;
                summary.Cell(row, 3).Style.Font.FontColor = XLColor.DarkGray;
            }
            row++;
        }
        summary.Columns().AdjustToContents();

        // ── Section sheets ───────────────────────────────────────────────
        var sheetCounter = 1;
        foreach (var section in document.Sections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = SafeSheetName(section.Heading, sheetCounter++);
            var ws = wb.Worksheets.Add(name);

            ws.Cell(1, 1).Value = section.Heading;
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;

            var startRow = 2;
            if (!string.IsNullOrEmpty(section.Description))
            {
                ws.Cell(startRow, 1).Value = section.Description;
                ws.Cell(startRow, 1).Style.Font.FontColor = XLColor.DarkGray;
                startRow++;
            }

            if (!string.IsNullOrEmpty(section.FreeTextHtml))
            {
                ws.Cell(startRow, 1).Value = StripHtml(section.FreeTextHtml);
                continue;
            }

            if (section.Columns.Count == 0) continue;

            var headerRow = startRow + 1;
            for (var c = 0; c < section.Columns.Count; c++)
            {
                var cell = ws.Cell(headerRow, c + 1);
                cell.Value = section.Columns[c].Header;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            }

            var sectionRows = section.Rows.ToList();
            for (var r = 0; r < sectionRows.Count; r++)
            {
                var rowCells = sectionRows[r];
                for (var c = 0; c < section.Columns.Count && c < rowCells.Count; c++)
                {
                    var col = section.Columns[c];
                    var target = ws.Cell(headerRow + 1 + r, c + 1);
                    AssignTypedValue(target, rowCells[c], col);
                }
                if (section.TotalsRow && r == sectionRows.Count - 1)
                {
                    ws.Row(headerRow + 1 + r).Style.Font.Bold = true;
                    ws.Row(headerRow + 1 + r).Style.Border.TopBorder = XLBorderStyleValues.Thin;
                }
            }
            ws.Columns().AdjustToContents();
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var bytes = ms.ToArray();

        var fileName = $"{CsvReportRenderer.SafeFilename(document.Title)}_{document.GeneratedAtUtc:yyyyMMdd-HHmm}.xlsx";
        return Task.FromResult(new RenderedReport(bytes, ReportFormat.Xlsx.MimeType(), fileName));
    }

    private static void AssignTypedValue(IXLCell target, object? value, ReportColumn col)
    {
        if (value is null) { target.Value = string.Empty; return; }
        switch (col.Type)
        {
            case ReportColumnType.Money:
                target.Value = Convert.ToDecimal(value);
                target.Style.NumberFormat.Format = col.Format ?? "#,##0.00";
                break;
            case ReportColumnType.Number:
                target.Value = Convert.ToDouble(value);
                target.Style.NumberFormat.Format = col.Format ?? "#,##0";
                break;
            case ReportColumnType.Percent:
                target.Value = Convert.ToDouble(value);
                target.Style.NumberFormat.Format = col.Format ?? "0.00%";
                break;
            case ReportColumnType.Date:
                if (value is DateTime d1) target.Value = d1;
                else target.Value = value.ToString();
                target.Style.DateFormat.Format = col.Format ?? "yyyy-mm-dd";
                break;
            case ReportColumnType.DateTime:
                if (value is DateTime d2) target.Value = d2;
                else target.Value = value.ToString();
                target.Style.DateFormat.Format = col.Format ?? "yyyy-mm-dd hh:mm:ss";
                break;
            case ReportColumnType.Boolean:
                target.Value = value is bool b && b;
                break;
            default:
                target.Value = value.ToString() ?? string.Empty;
                break;
        }
        if (col.AlignRight) target.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    private static string SafeSheetName(string raw, int fallback)
    {
        if (string.IsNullOrWhiteSpace(raw)) return $"Sheet{fallback}";
        var trimmed = raw.Length > 31 ? raw[..31] : raw;
        foreach (var c in new[] { ':', '\\', '/', '?', '*', '[', ']' })
            trimmed = trimmed.Replace(c, '_');
        return trimmed;
    }

    private static string StripHtml(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", string.Empty);
}
