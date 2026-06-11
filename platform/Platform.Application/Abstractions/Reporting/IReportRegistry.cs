// =============================================================================
// IReportRegistry / IReportDispatcher  (Platform.Application.Abstractions.Reporting)
// -----------------------------------------------------------------------------
// Registry lists every IReport discovered through DI. Dispatcher binds a
// parameter object out of a string-keyed dictionary (so admin forms + cron
// schedules can both feed it), resolves the right report, and pipes through
// the requested renderer.
//
// USAGE  (admin UI)
//   var rendered = await dispatcher.RunAsync(
//       reportKey: "canteen-daily-collection",
//       formData:  Request.Query.ToDictionary(...),
//       format:    ReportFormat.Pdf);
//   return File(rendered.Bytes, rendered.MimeType, rendered.SuggestedFileName);
// =============================================================================

namespace Platform.Application.Abstractions.Reporting;

public interface IReportRegistry
{
    IReadOnlyList<IReport> All();
    IReport? Find(string key);
}

public interface IReportDispatcher
{
    Task<RenderedReport> RunAsync(
        string reportKey,
        IReadOnlyDictionary<string, string?> formData,
        ReportFormat format,
        CancellationToken cancellationToken = default);
}
