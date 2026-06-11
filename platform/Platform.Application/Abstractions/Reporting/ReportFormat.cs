// =============================================================================
// ReportFormat  (Platform.Application.Abstractions.Reporting)
// -----------------------------------------------------------------------------
// Output formats every IReport definition supports out-of-the-box. New
// renderers register by binding a new IReportRenderer keyed to one of these.
//
// HTTP CONTENT TYPES + FILE EXTENSIONS  (used by the admin download endpoint)
//   Csv   text/csv                    .csv
//   Xlsx  application/vnd.openxmlformats-officedocument.spreadsheetml.sheet  .xlsx
//   Pdf   application/pdf             .pdf
//   Html  text/html                   .html
//   Json  application/json            .json   (debugging / partner integrations)
// =============================================================================

namespace Platform.Application.Abstractions.Reporting;

public enum ReportFormat
{
    Csv  = 1,
    Xlsx = 2,
    Pdf  = 3,
    Html = 4,
    Json = 5
}

public static class ReportFormatExtensions
{
    public static string MimeType(this ReportFormat f) => f switch
    {
        ReportFormat.Csv  => "text/csv",
        ReportFormat.Xlsx => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ReportFormat.Pdf  => "application/pdf",
        ReportFormat.Html => "text/html",
        ReportFormat.Json => "application/json",
        _ => "application/octet-stream"
    };

    public static string Extension(this ReportFormat f) => f switch
    {
        ReportFormat.Csv  => ".csv",
        ReportFormat.Xlsx => ".xlsx",
        ReportFormat.Pdf  => ".pdf",
        ReportFormat.Html => ".html",
        ReportFormat.Json => ".json",
        _ => ".bin"
    };
}
