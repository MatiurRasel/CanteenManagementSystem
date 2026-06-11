// =============================================================================
// ReportingExtensions  (Platform.Presentation.Reporting)
// -----------------------------------------------------------------------------
// One-line wiring for the whole reporting framework. Each product calls:
//
//   builder.Services.AddPlatformReporting();
//
// then registers its own reports via `services.AddScoped<IReport, MyReport>()`.
// =============================================================================

using Microsoft.Extensions.DependencyInjection;
using Platform.Application.Abstractions.Reporting;
using Platform.Infrastructure.Reporting;

namespace Platform.Presentation.Reporting;

public static class ReportingExtensions
{
    public static IServiceCollection AddPlatformReporting(this IServiceCollection services)
    {
        // Renderers (one per format, MIT/Apache only — true OSS).
        services.AddSingleton<IReportRenderer, CsvReportRenderer>();
        services.AddSingleton<IReportRenderer, XlsxReportRenderer>();
        services.AddSingleton<IReportRenderer, PdfReportRenderer>();
        services.AddSingleton<IReportRenderer, HtmlReportRenderer>();
        services.AddSingleton<IReportRenderer, JsonReportRenderer>();

        // Registry + dispatcher.
        services.AddScoped<IReportRegistry, ReportRegistry>();
        services.AddScoped<IReportDispatcher, ReportDispatcher>();

        // Distribution + scheduling.
        services.AddScoped<IReportDistributor, SmtpReportDistributor>();
        services.AddScoped<IReportScheduleAdminService, ReportScheduleAdminService>();
        services.AddHostedService<ReportSchedulerBackgroundService>();
        return services;
    }
}
