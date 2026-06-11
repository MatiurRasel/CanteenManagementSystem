// =============================================================================
// NbrVatRegisterReport  (CanteenManagementSystem.Infrastructure.Reports.Reports)
// -----------------------------------------------------------------------------
// Period VAT register in NBR Mushak 6.3 layout — taxable supply, VAT amount,
// gross. Aggregated from delivered Orders within the parameter date range.
// Tenant-scoped via the EF global filter; per-day rollups are pure SQL.
//
// Format note: VAT rate is taken from the per-tenant Vat.Rate setting (default
// 7.5% which is the canteen-applicable rate under the 2012 VAT Act); a tenant
// that uses a different rate (10% / 15%) just sets Vat.Rate accordingly. The
// rate is informational on this report — Orders.TotalAmount is the cash-in
// figure (VAT-inclusive); we back-derive Net + VAT.
// =============================================================================

using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Abstractions.Reporting;
using Platform.Application.Persistence;
using Platform.Domain.Tenancy;

namespace CanteenManagementSystem.Infrastructure.Reports.Reports;

public sealed class NbrVatRegisterReport : IReport<DateRangeParameters>
{
    public string Key                                  => "canteen-nbr-vat-register";
    public string DisplayName                          => "NBR VAT register (Mushak 6.3)";
    public string? Description                         => "Per-day VAT register for the chosen period — net / VAT / gross.";
    public string Group                                => "Compliance";
    public IReadOnlyList<string> RequiredPermissions   => new[] { "Reports.View" };
    public IReadOnlyList<ReportFormat> SupportedFormats => new[] { ReportFormat.Pdf, ReportFormat.Xlsx, ReportFormat.Csv, ReportFormat.Html, ReportFormat.Json };
    public Type ParameterType                           => typeof(DateRangeParameters);

    private readonly IAppDbContext  _db;
    private readonly ITenantContext _tenant;
    private readonly ITenantSettings _settings;

    public NbrVatRegisterReport(IAppDbContext db, ITenantContext tenant, ITenantSettings settings)
    {
        _db = db; _tenant = tenant; _settings = settings;
    }

    public Task<ReportDocument> GenerateAsync(object? parameters, CancellationToken cancellationToken = default)
        => GenerateAsync((parameters as DateRangeParameters) ?? new DateRangeParameters(), cancellationToken);

    public async Task<ReportDocument> GenerateAsync(DateRangeParameters parameters, CancellationToken cancellationToken = default)
    {
        var (from, to) = parameters.NormalizedUtc();
        var vatRateRaw = await _settings.GetAsync("Vat.Rate", defaultValue: "7.5", cancellationToken);
        if (!decimal.TryParse(vatRateRaw, System.Globalization.NumberStyles.Float,
                              System.Globalization.CultureInfo.InvariantCulture, out var vatRate))
        {
            vatRate = 7.5m;
        }
        var vatFactor = vatRate / (100m + vatRate); // back out VAT from a gross figure

        var perDay = await _db.Set<Order>()
            .AsNoTracking()
            .Where(o => o.OrderDate >= from && o.OrderDate < to
                        && o.Status == CanteenOrderStatus.Delivered)
            .GroupBy(o => o.OrderDate.Date)
            .Select(g => new
            {
                Day     = g.Key,
                Orders  = g.Count(),
                Gross   = g.Sum(o => o.TotalAmount)
            })
            .OrderBy(x => x.Day)
            .ToListAsync(cancellationToken);

        decimal totalGross  = perDay.Sum(d => d.Gross);
        decimal totalVat    = decimal.Round(totalGross * vatFactor, 2, MidpointRounding.AwayFromZero);
        decimal totalNet    = totalGross - totalVat;
        int     totalOrders = perDay.Sum(d => d.Orders);

        var rows = perDay.Select(d =>
        {
            var vat = decimal.Round(d.Gross * vatFactor, 2, MidpointRounding.AwayFromZero);
            var net = d.Gross - vat;
            return (IReadOnlyList<object?>)new object?[] { d.Day, d.Orders, net, vat, d.Gross };
        }).ToList();
        rows.Add(new object?[] { "TOTAL", totalOrders, totalNet, totalVat, totalGross });

        return new ReportDocument
        {
            Title       = "NBR VAT register (Mushak 6.3)",
            Subtitle    = $"{from:yyyy-MM-dd} → {to.AddDays(-1):yyyy-MM-dd} · VAT rate {vatRate.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} %",
            TenantName  = _tenant.ClientCode,
            Summary     = new[]
            {
                new ReportKpi("Gross sales", $"৳ {totalGross:N2}", $"{totalOrders} order(s)"),
                new ReportKpi("Net sales",   $"৳ {totalNet:N2}"),
                new ReportKpi("VAT payable", $"৳ {totalVat:N2}", $"{vatRate.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} %"),
                new ReportKpi("Days",        perDay.Count.ToString())
            },
            Sections    = new[]
            {
                new ReportSection
                {
                    Heading   = "Per-day register",
                    TotalsRow = true,
                    Columns   = new[]
                    {
                        new ReportColumn("Date",   ReportColumnType.Date,   WidthPercent: 22),
                        new ReportColumn("Orders", ReportColumnType.Number, WidthPercent: 12, AlignRight: true),
                        new ReportColumn("Net",    ReportColumnType.Money,  WidthPercent: 22, AlignRight: true),
                        new ReportColumn("VAT",    ReportColumnType.Money,  WidthPercent: 22, AlignRight: true),
                        new ReportColumn("Gross",  ReportColumnType.Money,  WidthPercent: 22, AlignRight: true)
                    },
                    Rows = rows
                }
            }
        };
    }
}
