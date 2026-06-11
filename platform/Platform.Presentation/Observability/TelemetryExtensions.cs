// =============================================================================
// TelemetryExtensions  (Presentation.Observability)
// -----------------------------------------------------------------------------
// Wires OpenTelemetry tracing + metrics into the host. Exports to OTLP by
// default (configurable to App Insights or Console for local dev).
//
// CONFIGURATION (appsettings / tenant settings)
//   Observability.ServiceName       Default "canteen-management"
//   Observability.OtlpEndpoint      e.g. https://otlp.eu01.nr-data.net (New Relic),
//                                       or https://api.honeycomb.io
//   Observability.OtlpProtocol      "grpc" (default) | "http/protobuf"
//   Observability.UseConsoleExporter true to dump traces to stdout in dev.
//
// CORRELATION
//   The HTTP server middleware automatically captures the W3C TraceParent
//   header; outbound HttpClient calls and EF Core queries are correlated as
//   child spans. The existing AuditTrail records the TraceIdentifier on each
//   write so a span in Honeycomb can pivot to its DB rows.
// =============================================================================

using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Platform.Presentation.Observability;

public static class TelemetryExtensions
{
    public const string ActivitySourceName = "CanteenManagementSystem";

    public static IServiceCollection AddCanteenTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        var serviceName    = configuration["Observability:ServiceName"] ?? "canteen-management";
        var serviceVersion = typeof(TelemetryExtensions).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        var otlpEndpoint   = configuration["Observability:OtlpEndpoint"];
        var useConsole     = bool.TryParse(configuration["Observability:UseConsoleExporter"], out var c) && c;

        services.AddOpenTelemetry()
            .ConfigureResource(rb => rb
                .AddService(serviceName, serviceVersion: serviceVersion)
                .AddAttributes(new KeyValuePair<string, object>[]
                {
                    new("deployment.environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production")
                }))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(ActivitySourceName)
                    .AddAspNetCoreInstrumentation(o => o.RecordException = true)
                    .AddHttpClientInstrumentation(o => o.RecordException = true)
                    .AddEntityFrameworkCoreInstrumentation(o => { o.SetDbStatementForText = true; });

                if (useConsole) tracing.AddConsoleExporter();
                if (!string.IsNullOrEmpty(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter("Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.Server.Kestrel", "System.Net.Http")
                    .AddMeter(ActivitySourceName)
                    .AddMeter("Platform.Webhooks", "Platform.Payments");

                if (useConsole) metrics.AddConsoleExporter();
                if (!string.IsNullOrEmpty(otlpEndpoint))
                {
                    metrics.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                }

                // Prometheus scrape endpoint — wired via app.MapPrometheusScrapingEndpoint()
                // in the host. Toggle by setting Observability:EnablePrometheus = false.
                var enablePrometheus = !bool.TryParse(configuration["Observability:EnablePrometheus"], out var ep) || ep;
                if (enablePrometheus) metrics.AddPrometheusExporter();
            });

        return services;
    }
}
