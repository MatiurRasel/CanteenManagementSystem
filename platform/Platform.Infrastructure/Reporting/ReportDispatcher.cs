// =============================================================================
// ReportDispatcher  (Platform.Infrastructure.Reporting)
// -----------------------------------------------------------------------------
// Binds the report's typed parameter object from a string-keyed dictionary
// (System.Text.Json round-trip handles type coercion: "2026-06-01" → DateTime,
// "true" → bool, "12" → int) and pipes through the matching renderer.
//
// PERMISSION CHECK
//   The caller is expected to have already verified IReport.RequiredPermissions
//   via a policy filter — the dispatcher itself doesn't authorise.
// =============================================================================

using System.Text.Json;
using Platform.Application.Abstractions.Reporting;

namespace Platform.Infrastructure.Reporting;

public sealed class ReportDispatcher : IReportDispatcher
{
    private static readonly JsonSerializerOptions BindOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    private readonly IReportRegistry _registry;
    private readonly Dictionary<ReportFormat, IReportRenderer> _renderers;

    public ReportDispatcher(IReportRegistry registry, IEnumerable<IReportRenderer> renderers)
    {
        _registry  = registry;
        _renderers = renderers.ToDictionary(r => r.Format);
    }

    public async Task<RenderedReport> RunAsync(
        string reportKey,
        IReadOnlyDictionary<string, string?> formData,
        ReportFormat format,
        CancellationToken cancellationToken = default)
    {
        var report = _registry.Find(reportKey)
            ?? throw new InvalidOperationException($"Unknown report '{reportKey}'.");

        if (!report.SupportedFormats.Contains(format))
            throw new InvalidOperationException($"Report '{report.Key}' does not support format {format}.");

        if (!_renderers.TryGetValue(format, out var renderer))
            throw new InvalidOperationException($"No renderer registered for format {format}.");

        var parameters = BindParameters(report.ParameterType, formData);
        var document   = await report.GenerateAsync(parameters, cancellationToken);
        return await renderer.RenderAsync(document, cancellationToken);
    }

    private static object? BindParameters(Type t, IReadOnlyDictionary<string, string?> formData)
    {
        if (t == typeof(object)) return null;
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in formData)
        {
            if (string.IsNullOrEmpty(kv.Key)) continue;
            dict[kv.Key] = kv.Value;
        }
        var json = JsonSerializer.Serialize(dict);
        return JsonSerializer.Deserialize(json, t, BindOptions) ?? Activator.CreateInstance(t);
    }
}
