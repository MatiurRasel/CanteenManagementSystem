// =============================================================================
// IReportRenderer  (Platform.Application.Abstractions.Reporting)
// -----------------------------------------------------------------------------
// Format-specific renderer. One impl per ReportFormat:
//
//   CsvReportRenderer    → bytes UTF-8, one file per section concatenated
//   XlsxReportRenderer   → ClosedXML workbook, one worksheet per section
//   PdfReportRenderer    → PdfSharpCore document, A4 portrait
//   HtmlReportRenderer   → inline-styled HTML string
//   JsonReportRenderer   → System.Text.Json
//
// Renderers know NOTHING about which report they are rendering — they only
// consume ReportDocument. Adding a brand-new format = one renderer class.
// =============================================================================

namespace Platform.Application.Abstractions.Reporting;

public interface IReportRenderer
{
    /// <summary>The format this renderer produces.</summary>
    ReportFormat Format { get; }

    /// <summary>Render the document. Returns rendered bytes + suggested filename.</summary>
    Task<RenderedReport> RenderAsync(ReportDocument document, CancellationToken cancellationToken = default);
}

public sealed record RenderedReport(byte[] Bytes, string MimeType, string SuggestedFileName);
