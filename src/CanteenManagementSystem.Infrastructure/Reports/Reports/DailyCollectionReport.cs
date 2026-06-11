// =============================================================================
// DailyCollectionReport  (CanteenManagementSystem.Infrastructure.Reports.Reports)
// -----------------------------------------------------------------------------
// First canteen-specific report on the new framework. Same query that powers
// /admin/reports (IReportingService.GetDailyCollectionAsync), now shaped into
// a format-agnostic ReportDocument so PDF / XLSX / CSV / HTML all work.
// =============================================================================

using Platform.Application.Abstractions.Reporting;
using Platform.Application.Abstractions.Reports;
using Platform.Domain.Tenancy;

namespace CanteenManagementSystem.Infrastructure.Reports.Reports;

public sealed class DailyCollectionReport : IReport<DateRangeParameters>
{
    public string Key                                  => "canteen-daily-collection";
    public string DisplayName                          => "Daily collection";
    public string? Description                         => "Per-day revenue, order count, refunds.";
    public string Group                                => "Sales";
    public IReadOnlyList<string> RequiredPermissions   => new[] { "Reports.View" };
    public IReadOnlyList<ReportFormat> SupportedFormats => new[] { ReportFormat.Pdf, ReportFormat.Xlsx, ReportFormat.Csv, ReportFormat.Html, ReportFormat.Json };
    public Type ParameterType                           => typeof(DateRangeParameters);

    private readonly IReportingService _service;
    private readonly ITenantContext _tenant;

    public DailyCollectionReport(IReportingService service, ITenantContext tenant)
    {
        _service = service; _tenant = tenant;
    }

    public Task<ReportDocument> GenerateAsync(object? parameters, CancellationToken cancellationToken = default)
        => GenerateAsync((parameters as DateRangeParameters) ?? new DateRangeParameters(), cancellationToken);

    public async Task<ReportDocument> GenerateAsync(DateRangeParameters parameters, CancellationToken cancellationToken = default)
    {
        var (from, to) = parameters.NormalizedUtc();
        var data = await _service.GetDailyCollectionAsync(from, to, cancellationToken);

        var rows = data.Days.Select(d => (IReadOnlyList<object?>)new object?[]
        {
            d.Date, d.Orders, d.Revenue, d.Refunded, d.Revenue - d.Refunded
        }).ToList();

        // Totals row.
        var totals = new object?[]
        {
            "TOTAL",
            data.OrderCount,
            data.Total,
            data.Days.Sum(d => d.Refunded),
            data.Total - data.Days.Sum(d => d.Refunded)
        };
        rows.Add(totals);

        return new ReportDocument
        {
            Title       = "Daily collection",
            Subtitle    = $"{from:yyyy-MM-dd} → {to:yyyy-MM-dd}",
            TenantName  = _tenant.ClientCode,
            Summary     = new[]
            {
                new ReportKpi("Total revenue", $"৳ {data.Total:N0}", $"{data.Days.Count} day(s)"),
                new ReportKpi("Orders",        data.OrderCount.ToString("N0"),
                              data.OrderCount == 0 ? null : $"avg ৳ {(data.Total / data.OrderCount):N0}"),
                new ReportKpi("Refunded",      $"৳ {data.Days.Sum(d => d.Refunded):N0}")
            },
            Sections    = new[]
            {
                new ReportSection
                {
                    Heading   = "Per-day breakdown",
                    TotalsRow = true,
                    Columns   = new[]
                    {
                        new ReportColumn("Date",     ReportColumnType.Date,     WidthPercent: 20),
                        new ReportColumn("Orders",   ReportColumnType.Number,   WidthPercent: 15, AlignRight: true),
                        new ReportColumn("Revenue",  ReportColumnType.Money,    WidthPercent: 22, AlignRight: true),
                        new ReportColumn("Refunded", ReportColumnType.Money,    WidthPercent: 22, AlignRight: true),
                        new ReportColumn("Net",      ReportColumnType.Money,    WidthPercent: 21, AlignRight: true)
                    },
                    Rows = rows
                }
            }
        };
    }
}
