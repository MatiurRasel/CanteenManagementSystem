# Observability

## OpenTelemetry pipeline

* **Traces** — ASP.NET Core requests, outbound HttpClient calls (every gateway
  + Twilio + SMTP), EF Core query spans.
* **Metrics** — Kestrel, HTTP server, HttpClient, AspNetCore.Hosting meters.
* **Logs** — Serilog → console + rolling file. Correlation IDs flow through
  the `TraceParent` header automatically.

## Exporting

Set `Observability:OtlpEndpoint` in appsettings or tenant settings, e.g.:

* `http://otel-collector:4317` (local docker-compose)
* `https://otlp.eu01.nr-data.net` (New Relic)
* `https://api.honeycomb.io` (Honeycomb)
* `https://ingest.signoz.io` (SigNoz cloud)

Set `Observability:UseConsoleExporter=true` for local dev to see spans on
stdout.

## Correlation with the audit trail

Every `AuditEntry` row stamps the current `HttpContext.TraceIdentifier`. A
trace in Honeycomb can pivot directly to its DB rows by searching audit by
`CorrelationId`.

## Adding custom spans

```csharp
private static readonly ActivitySource _activity = new(TelemetryExtensions.ActivitySourceName);
using var span = _activity.StartActivity("Payments.bKash.Initiate");
span?.SetTag("amount", amount);
// ... work ...
```
