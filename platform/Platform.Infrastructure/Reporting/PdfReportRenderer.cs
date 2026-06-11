// =============================================================================
// PdfReportRenderer  (Platform.Infrastructure.Reporting)
// -----------------------------------------------------------------------------
// PdfSharpCore (MIT) → A4 portrait PDF. Hand-laid because PdfSharpCore is
// drawing-primitive and we don't want a heavy templating dependency.
//
// LAYOUT
//   Title (16pt bold)
//   Subtitle (10pt grey)
//   Tenant + generated stamp (8pt grey)
//   ── separator ──
//   KPI block: two-column "Label: Value" lines (10pt)
//   Each section:
//     section heading (12pt bold)
//     optional description (9pt grey)
//     table grid with bold header, money columns right-aligned,
//     totals row bolded + ruled top.
//   Page breaks happen automatically when y > pageHeight - margin.
//   Page numbers in footer.
// =============================================================================

using System.Globalization;
using PdfSharpCore.Drawing;
using PdfSharpCore.Fonts;
using PdfSharpCore.Pdf;
using PdfSharpCore.Utils;
using Platform.Application.Abstractions.Reporting;

namespace Platform.Infrastructure.Reporting;

public sealed class PdfReportRenderer : IReportRenderer
{
    public ReportFormat Format => ReportFormat.Pdf;

    static PdfReportRenderer()
    {
        // PdfSharpCore needs a font resolver — Linux/Windows agnostic.
        // FontResolver bundles standard fonts so we don't depend on system fonts.
        if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.FontResolver = new FontResolver();
        }
    }

    public Task<RenderedReport> RenderAsync(ReportDocument document, CancellationToken cancellationToken = default)
    {
        using var pdf = new PdfDocument();
        pdf.Info.Title  = document.Title;
        pdf.Info.Author = document.TenantName ?? "Canteen Management System";

        var margin = 36.0;                       // 0.5 in
        var page   = AddPage(pdf);
        var gfx    = XGraphics.FromPdfPage(page);
        var pen    = new XPen(XColors.LightGray, 0.5);
        var fontTitle    = new XFont("Arial", 16, XFontStyle.Bold);
        var fontSubtitle = new XFont("Arial", 10, XFontStyle.Regular);
        var fontGrey     = new XFont("Arial", 8,  XFontStyle.Regular);
        var fontH2       = new XFont("Arial", 12, XFontStyle.Bold);
        var fontDesc     = new XFont("Arial", 9,  XFontStyle.Italic);
        var fontKpi      = new XFont("Arial", 10, XFontStyle.Regular);
        var fontKpiBold  = new XFont("Arial", 10, XFontStyle.Bold);
        var fontTable    = new XFont("Arial", 9,  XFontStyle.Regular);
        var fontTableHdr = new XFont("Arial", 9,  XFontStyle.Bold);

        var pageNo  = 1;
        var width   = page.Width  - 2 * margin;
        var bottom  = page.Height - margin - 22;       // leave room for footer
        var y       = margin;

        gfx.DrawString(document.Title, fontTitle, XBrushes.Black, margin, y + 16);
        y += 22;
        if (!string.IsNullOrWhiteSpace(document.Subtitle))
        {
            gfx.DrawString(document.Subtitle!, fontSubtitle, XBrushes.DimGray, margin, y + 12);
            y += 16;
        }
        gfx.DrawString($"Generated: {document.GeneratedAtUtc:u}" +
                       (string.IsNullOrEmpty(document.TenantName) ? "" : $"  ·  Tenant: {document.TenantName}"),
            fontGrey, XBrushes.DarkGray, margin, y + 9);
        y += 14;
        gfx.DrawLine(pen, margin, y, page.Width - margin, y);
        y += 10;

        // KPI block
        if (document.Summary.Count > 0)
        {
            foreach (var k in document.Summary)
            {
                gfx.DrawString(k.Label, fontKpiBold, XBrushes.Black, margin, y + 10);
                gfx.DrawString(k.Value, fontKpi, XBrushes.Black, margin + 160, y + 10);
                if (!string.IsNullOrEmpty(k.Hint))
                    gfx.DrawString(k.Hint, fontGrey, XBrushes.DimGray, margin + 320, y + 10);
                y += 14;
                (y, page, gfx, pageNo) = MaybeNewPage(pdf, page, gfx, pageNo, y, bottom);
            }
            y += 6;
        }

        // Sections
        foreach (var section in document.Sections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            (y, page, gfx, pageNo) = MaybeNewPage(pdf, page, gfx, pageNo, y + 6, bottom, force: y > bottom - 60);
            gfx.DrawString(section.Heading, fontH2, XBrushes.Black, margin, y + 12);
            y += 16;
            if (!string.IsNullOrEmpty(section.Description))
            {
                gfx.DrawString(section.Description!, fontDesc, XBrushes.DimGray, margin, y + 10);
                y += 14;
            }

            if (!string.IsNullOrEmpty(section.FreeTextHtml))
            {
                var text = StripHtml(section.FreeTextHtml!);
                foreach (var line in WrapText(text, fontTable, width, gfx))
                {
                    gfx.DrawString(line, fontTable, XBrushes.Black, margin, y + 10);
                    y += 12;
                    (y, page, gfx, pageNo) = MaybeNewPage(pdf, page, gfx, pageNo, y, bottom);
                }
                y += 6;
                continue;
            }

            if (section.Columns.Count == 0) continue;
            var columnWidths = ComputeColumnWidths(section.Columns, width);

            // Header
            DrawTableRow(gfx, fontTableHdr, XBrushes.White, XBrushes.Black,
                section.Columns.Select(c => c.Header).Cast<object?>().ToList(),
                section.Columns, columnWidths, margin, y, headerFill: new XSolidBrush(XColor.FromArgb(220, 230, 245)));
            y += 16;

            // Body
            for (var r = 0; r < section.Rows.Count; r++)
            {
                (y, page, gfx, pageNo) = MaybeNewPage(pdf, page, gfx, pageNo, y, bottom);
                var isTotal = section.TotalsRow && r == section.Rows.Count - 1;
                DrawTableRow(gfx, isTotal ? fontTableHdr : fontTable, XBrushes.Black, XBrushes.Black,
                    section.Rows[r], section.Columns, columnWidths, margin, y);
                if (isTotal)
                {
                    gfx.DrawLine(pen, margin, y - 1, page.Width - margin, y - 1);
                }
                y += 14;
            }
            y += 8;
        }

        // Footer (page numbers) — drawn on every accumulated page.
        for (var i = 0; i < pdf.PageCount; i++)
        {
            var p = pdf.Pages[i];
            using var g = XGraphics.FromPdfPage(p);
            g.DrawString($"Page {i + 1} of {pdf.PageCount}", fontGrey, XBrushes.DimGray,
                new XRect(0, p.Height - 22, p.Width, 12), XStringFormats.Center);
        }

        using var ms = new MemoryStream();
        pdf.Save(ms, false);
        var bytes = ms.ToArray();
        return Task.FromResult(new RenderedReport(
            bytes, ReportFormat.Pdf.MimeType(),
            $"{CsvReportRenderer.SafeFilename(document.Title)}_{document.GeneratedAtUtc:yyyyMMdd-HHmm}.pdf"));
    }

    private static PdfPage AddPage(PdfDocument pdf)
    {
        var p = pdf.AddPage();
        p.Size = PdfSharpCore.PageSize.A4;
        p.Orientation = PdfSharpCore.PageOrientation.Portrait;
        return p;
    }

    private static (double y, PdfPage page, XGraphics gfx, int pageNo) MaybeNewPage(
        PdfDocument pdf, PdfPage page, XGraphics gfx, int pageNo, double y, double bottom, bool force = false)
    {
        if (!force && y < bottom) return (y, page, gfx, pageNo);
        gfx.Dispose();
        var newPage = AddPage(pdf);
        var newGfx = XGraphics.FromPdfPage(newPage);
        return (36.0, newPage, newGfx, pageNo + 1);
    }

    private static double[] ComputeColumnWidths(IReadOnlyList<ReportColumn> columns, double total)
    {
        var explicitTotal = columns.Where(c => c.WidthPercent.HasValue).Sum(c => c.WidthPercent!.Value);
        var explicitCount = columns.Count(c => c.WidthPercent.HasValue);
        var remaining = 100 - explicitTotal;
        var implicitEach = columns.Count - explicitCount == 0 ? 0 : remaining / (columns.Count - explicitCount);
        return columns.Select(c => total * (c.WidthPercent ?? implicitEach) / 100.0).ToArray();
    }

    private static void DrawTableRow(
        XGraphics gfx, XFont font, XBrush textBrush, XBrush textBlack,
        IReadOnlyList<object?> row, IReadOnlyList<ReportColumn> cols, double[] widths,
        double left, double y, XBrush? headerFill = null)
    {
        if (headerFill is not null)
            gfx.DrawRectangle(headerFill, left, y, widths.Sum(), 14);

        var x = left;
        for (var c = 0; c < cols.Count; c++)
        {
            var w = widths[c];
            var col = cols[c];
            var value = c < row.Count ? CsvReportRenderer.FormatCell(row[c], col) : string.Empty;
            var fmt = col.AlignRight || col.Type is ReportColumnType.Money or ReportColumnType.Number or ReportColumnType.Percent
                ? XStringFormats.CenterRight
                : XStringFormats.CenterLeft;
            gfx.DrawString(value, font,
                headerFill is not null ? XBrushes.Black : textBlack,
                new XRect(x + 2, y - 2, w - 4, 16), fmt);
            x += w;
        }
    }

    private static IEnumerable<string> WrapText(string text, XFont font, double maxWidth, XGraphics gfx)
    {
        foreach (var line in text.Split('\n'))
        {
            var words = line.Split(' ');
            var buffer = string.Empty;
            foreach (var w in words)
            {
                var candidate = string.IsNullOrEmpty(buffer) ? w : buffer + ' ' + w;
                if (gfx.MeasureString(candidate, font).Width > maxWidth && !string.IsNullOrEmpty(buffer))
                {
                    yield return buffer;
                    buffer = w;
                }
                else buffer = candidate;
            }
            if (!string.IsNullOrEmpty(buffer)) yield return buffer;
        }
    }

    private static string StripHtml(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", string.Empty);
}
