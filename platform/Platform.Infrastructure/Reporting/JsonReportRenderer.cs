// =============================================================================
// JsonReportRenderer  (Platform.Infrastructure.Reporting)
// -----------------------------------------------------------------------------
// Serialises a ReportDocument straight to JSON. Useful for:
//   * partner integrators reading data from the admin API
//   * debugging (the in-memory shape is easy to inspect)
//   * downstream warehouse loaders (Power BI / Metabase pull JSON cheaply)
// =============================================================================

using System.Text;
using System.Text.Json;
using Platform.Application.Abstractions.Reporting;

namespace Platform.Infrastructure.Reporting;

public sealed class JsonReportRenderer : IReportRenderer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public ReportFormat Format => ReportFormat.Json;

    public Task<RenderedReport> RenderAsync(ReportDocument document, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(document, Options);
        var bytes = Encoding.UTF8.GetBytes(json);
        return Task.FromResult(new RenderedReport(bytes, ReportFormat.Json.MimeType(),
            $"{CsvReportRenderer.SafeFilename(document.Title)}_{document.GeneratedAtUtc:yyyyMMdd-HHmm}.json"));
    }
}
