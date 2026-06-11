// =============================================================================
// PopularItemsReport  (CanteenManagementSystem.Infrastructure.Reports.Reports)
// -----------------------------------------------------------------------------
// Top N items by units sold across the period.
// =============================================================================

using Platform.Application.Abstractions.Reporting;
using Platform.Application.Abstractions.Reports;
using Platform.Domain.Tenancy;

namespace CanteenManagementSystem.Infrastructure.Reports.Reports;

public sealed class PopularItemsParameters : DateRangeParameters
{
    public int Top { get; set; } = 10;
}

public sealed class PopularItemsReport : IReport<PopularItemsParameters>
{
    public string Key                                  => "canteen-popular-items";
    public string DisplayName                          => "Popular items";
    public string? Description                         => "Top items by quantity sold.";
    public string Group                                => "Sales";
    public IReadOnlyList<string> RequiredPermissions   => new[] { "Reports.View" };
    public IReadOnlyList<ReportFormat> SupportedFormats => new[] { ReportFormat.Pdf, ReportFormat.Xlsx, ReportFormat.Csv, ReportFormat.Html, ReportFormat.Json };
    public Type ParameterType                           => typeof(PopularItemsParameters);

    private readonly IReportingService _service;
    private readonly ITenantContext _tenant;

    public PopularItemsReport(IReportingService service, ITenantContext tenant)
    {
        _service = service; _tenant = tenant;
    }

    public Task<ReportDocument> GenerateAsync(object? parameters, CancellationToken cancellationToken = default)
        => GenerateAsync((parameters as PopularItemsParameters) ?? new PopularItemsParameters(), cancellationToken);

    public async Task<ReportDocument> GenerateAsync(PopularItemsParameters parameters, CancellationToken cancellationToken = default)
    {
        var (from, to) = parameters.NormalizedUtc();
        var top = Math.Clamp(parameters.Top, 1, 100);
        var data = await _service.GetPopularItemsAsync(from, to, top, cancellationToken);

        var rows = data.Select((r, i) => (IReadOnlyList<object?>)new object?[] {
            i + 1, r.ItemName, r.QuantitySold, r.Revenue
        }).ToList();

        return new ReportDocument
        {
            Title      = $"Top {top} popular items",
            Subtitle   = $"{from:yyyy-MM-dd} → {to:yyyy-MM-dd}",
            TenantName = _tenant.ClientCode,
            Summary    = new[]
            {
                new ReportKpi("Items in report", data.Count.ToString()),
                new ReportKpi("Units sold",      data.Sum(r => r.QuantitySold).ToString("N0")),
                new ReportKpi("Revenue",         $"৳ {data.Sum(r => r.Revenue):N0}")
            },
            Sections = new[] { new ReportSection
            {
                Heading = "Ranking",
                Columns = new[]
                {
                    new ReportColumn("#",         ReportColumnType.Number, WidthPercent: 8,  AlignRight: true),
                    new ReportColumn("Item",      ReportColumnType.Text,   WidthPercent: 52),
                    new ReportColumn("Qty sold",  ReportColumnType.Number, WidthPercent: 18, AlignRight: true),
                    new ReportColumn("Revenue",   ReportColumnType.Money,  WidthPercent: 22, AlignRight: true)
                },
                Rows = rows
            }}
        };
    }
}
