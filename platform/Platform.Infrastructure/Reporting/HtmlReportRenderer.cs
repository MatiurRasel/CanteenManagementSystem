// =============================================================================
// HtmlReportRenderer  (Platform.Infrastructure.Reporting)
// -----------------------------------------------------------------------------
// Renders ReportDocument → inline-styled HTML (single self-contained file).
// No external CSS / JS so the output can be emailed or saved to disk and
// opened anywhere.
//
// USES
//   * Email attachments where the recipient prefers HTML over PDF.
//   * Quick admin preview before downloading the heavier PDF.
//   * Embedded in a printable view (browser → "Print" → physical paper).
// =============================================================================

using System.Globalization;
using System.Net;
using System.Text;
using Platform.Application.Abstractions.Reporting;

namespace Platform.Infrastructure.Reporting;

public sealed class HtmlReportRenderer : IReportRenderer
{
    public ReportFormat Format => ReportFormat.Html;

    public Task<RenderedReport> RenderAsync(ReportDocument document, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\" />");
        sb.Append("<title>").Append(WebUtility.HtmlEncode(document.Title)).Append("</title>");
        sb.Append(@"<style>
            body { font: 14px/1.5 'Inter', system-ui, -apple-system, sans-serif; color: #1f2937; padding: 24px; max-width: 960px; margin: 0 auto; }
            h1 { font-size: 22px; margin: 0 0 4px; }
            h2 { font-size: 16px; margin: 24px 0 8px; padding-bottom: 4px; border-bottom: 1px solid #e5e7eb; }
            .subtitle { color: #6b7280; margin-bottom: 4px; }
            .meta { color: #9ca3af; font-size: 12px; margin-bottom: 18px; }
            .kpi { display: grid; grid-template-columns: 200px 1fr 1fr; gap: 4px 16px; margin: 10px 0 18px; }
            .kpi b { color: #111827; }
            .kpi i { color: #6b7280; font-style: normal; font-size: 12px; }
            table { width: 100%; border-collapse: collapse; font-size: 13px; margin-bottom: 14px; }
            th, td { padding: 6px 8px; text-align: left; border-bottom: 1px solid #e5e7eb; }
            th { background: #f3f4f6; font-weight: 600; }
            td.num, th.num { text-align: right; font-variant-numeric: tabular-nums; }
            tr.total td { font-weight: 700; border-top: 1px solid #111827; }
            .desc { color: #6b7280; font-size: 12px; margin-bottom: 8px; }
            @media print { body { padding: 0; max-width: none; } }
        </style></head><body>");
        sb.Append("<h1>").Append(WebUtility.HtmlEncode(document.Title)).Append("</h1>");
        if (!string.IsNullOrWhiteSpace(document.Subtitle))
            sb.Append("<div class=\"subtitle\">").Append(WebUtility.HtmlEncode(document.Subtitle!)).Append("</div>");
        sb.Append("<div class=\"meta\">Generated ").Append(document.GeneratedAtUtc.ToString("u"));
        if (!string.IsNullOrEmpty(document.TenantName))
            sb.Append(" · Tenant: ").Append(WebUtility.HtmlEncode(document.TenantName!));
        sb.Append("</div>");

        if (document.Summary.Count > 0)
        {
            sb.Append("<div class=\"kpi\">");
            foreach (var k in document.Summary)
            {
                sb.Append("<b>").Append(WebUtility.HtmlEncode(k.Label)).Append("</b>");
                sb.Append("<span>").Append(WebUtility.HtmlEncode(k.Value)).Append("</span>");
                sb.Append("<i>").Append(WebUtility.HtmlEncode(k.Hint ?? string.Empty)).Append("</i>");
            }
            sb.Append("</div>");
        }

        foreach (var section in document.Sections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sb.Append("<h2>").Append(WebUtility.HtmlEncode(section.Heading)).Append("</h2>");
            if (!string.IsNullOrEmpty(section.Description))
                sb.Append("<div class=\"desc\">").Append(WebUtility.HtmlEncode(section.Description!)).Append("</div>");

            if (!string.IsNullOrEmpty(section.FreeTextHtml))
            {
                sb.Append(section.FreeTextHtml);
                continue;
            }
            if (section.Columns.Count == 0) continue;

            sb.Append("<table><thead><tr>");
            foreach (var c in section.Columns)
            {
                var cls = c.AlignRight || c.Type is ReportColumnType.Money or ReportColumnType.Number or ReportColumnType.Percent ? " class=\"num\"" : "";
                sb.Append("<th").Append(cls).Append('>').Append(WebUtility.HtmlEncode(c.Header)).Append("</th>");
            }
            sb.Append("</tr></thead><tbody>");

            for (var r = 0; r < section.Rows.Count; r++)
            {
                var isTotal = section.TotalsRow && r == section.Rows.Count - 1;
                sb.Append("<tr").Append(isTotal ? " class=\"total\"" : "").Append('>');
                for (var c = 0; c < section.Columns.Count; c++)
                {
                    var col = section.Columns[c];
                    var cls = col.AlignRight || col.Type is ReportColumnType.Money or ReportColumnType.Number or ReportColumnType.Percent ? " class=\"num\"" : "";
                    var cell = c < section.Rows[r].Count ? section.Rows[r][c] : null;
                    sb.Append("<td").Append(cls).Append('>')
                      .Append(WebUtility.HtmlEncode(CsvReportRenderer.FormatCell(cell, col)))
                      .Append("</td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table>");
        }

        sb.Append("</body></html>");
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return Task.FromResult(new RenderedReport(bytes, ReportFormat.Html.MimeType(),
            $"{CsvReportRenderer.SafeFilename(document.Title)}_{document.GeneratedAtUtc:yyyyMMdd-HHmm}.html"));
    }
}
