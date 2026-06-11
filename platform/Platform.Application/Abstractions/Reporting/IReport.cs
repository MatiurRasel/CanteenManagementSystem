// =============================================================================
// IReport / IReport<TParams>  (Platform.Application.Abstractions.Reporting)
// -----------------------------------------------------------------------------
// Strongly-typed report definition. Every report is one class with:
//
//   Key          stable, dash-separated identifier ("canteen-daily-collection")
//   DisplayName  shown in the admin Reports page
//   Group        ("Sales", "Wallet", "Audit", "Inventory") for sidebar grouping
//   Permissions  permission codes the caller must hold (e.g. "Reports.View")
//   GenerateAsync(parameters, ct) → ReportDocument
//
// Parameter binding
//   The framework feeds the report a typed parameter object hydrated from
//   query-string / form values. Products define their own POCO; the
//   dispatcher binds via System.Text.Json so admin UIs can build forms
//   dynamically by reading public properties.
//
// Sister products
//   Each product (canteen, rent, clinic) ships one class per report,
//   registered in DI via `services.AddScoped<IReport, MyReport>()`. The
//   platform discovers them through IReportRegistry without further wiring.
// =============================================================================

namespace Platform.Application.Abstractions.Reporting;

public interface IReport
{
    /// <summary>Stable identifier ("canteen-daily-collection"). Used in URLs + schedules.</summary>
    string Key { get; }

    /// <summary>Short name for the admin list.</summary>
    string DisplayName { get; }

    /// <summary>Optional sentence describing what this report covers.</summary>
    string? Description { get; }

    /// <summary>Sidebar grouping bucket. Free-text — e.g. "Sales", "Wallet", "Audit", "Compliance".</summary>
    string Group { get; }

    /// <summary>Permissions the caller must have. ANY of them satisfies the check (OR).</summary>
    IReadOnlyList<string> RequiredPermissions { get; }

    /// <summary>Default formats this report renders cleanly into. UI hides others.</summary>
    IReadOnlyList<ReportFormat> SupportedFormats { get; }

    /// <summary>Parameter shape (typeof(MyParams)). Used by the admin UI to build a form.</summary>
    Type ParameterType { get; }

    /// <summary>Run the query and produce a format-agnostic document.</summary>
    Task<ReportDocument> GenerateAsync(object? parameters, CancellationToken cancellationToken = default);
}

/// Typed variant — strongly-typed parameter binding.
public interface IReport<TParameters> : IReport
    where TParameters : class, new()
{
    Task<ReportDocument> GenerateAsync(TParameters parameters, CancellationToken cancellationToken = default);
}

/// Common parameter base. Most reports accept a date range; reports
/// requiring more bind their own POCO with extra fields.
public class DateRangeParameters
{
    public DateTime? From { get; set; }
    public DateTime? To   { get; set; }

    public (DateTime From, DateTime To) NormalizedUtc(int defaultWindowDays = 7)
    {
        var t = (To   ?? DateTime.UtcNow).Date;
        var f = (From ?? t.AddDays(-defaultWindowDays)).Date;
        if (t < f) (f, t) = (t, f);
        return (f, t);
    }
}
