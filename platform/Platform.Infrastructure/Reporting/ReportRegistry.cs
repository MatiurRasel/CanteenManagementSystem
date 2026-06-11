// =============================================================================
// ReportRegistry  (Platform.Infrastructure.Reporting)
// -----------------------------------------------------------------------------
// Light DI-backed registry. Every IReport registered in the container shows up
// here automatically — products don't need to call any "register" extension.
//
//   services.AddScoped<IReport, MyProductReport>();   // ← enough.
// =============================================================================

using Platform.Application.Abstractions.Reporting;

namespace Platform.Infrastructure.Reporting;

public sealed class ReportRegistry : IReportRegistry
{
    private readonly Dictionary<string, IReport> _byKey;

    public ReportRegistry(IEnumerable<IReport> reports)
    {
        _byKey = reports
            .GroupBy(r => r.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<IReport> All() => _byKey.Values
        .OrderBy(r => r.Group, StringComparer.OrdinalIgnoreCase)
        .ThenBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public IReport? Find(string key)
        => string.IsNullOrEmpty(key) ? null : (_byKey.TryGetValue(key, out var r) ? r : null);
}
