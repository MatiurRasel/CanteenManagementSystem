// =============================================================================
// WalletBalanceDistributionReport
// -----------------------------------------------------------------------------
// Snapshot of wallet balances grouped into spend buckets. Helps admins
// understand how concentrated wallet money is — is most credit sitting in a
// few large accounts, or is it spread evenly? Drives recharge campaigns
// (target the low-balance tail) and float-management decisions.
//
// ADR 0004: IReadOnlyRepository<UserBalance> — pure read.
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions.Reporting;
using Platform.Application.Persistence;
using Platform.Domain.Tenancy;
using CanteenManagementSystem.Domain.Wallets;

namespace CanteenManagementSystem.Infrastructure.Reports.Reports;

public sealed class WalletBalanceDistributionReport : IReport<DateRangeParameters>
{
    public string Key                                   => "canteen-wallet-balance-distribution";
    public string DisplayName                           => "Wallet balance distribution";
    public string? Description                          => "Active wallets grouped by available balance.";
    public string Group                                 => "Wallet";
    public IReadOnlyList<string> RequiredPermissions    => new[] { "Reports.View" };
    public IReadOnlyList<ReportFormat> SupportedFormats => new[] { ReportFormat.Pdf, ReportFormat.Xlsx, ReportFormat.Csv, ReportFormat.Html, ReportFormat.Json };
    public Type ParameterType                           => typeof(DateRangeParameters);

    private readonly IReadOnlyRepository<UserBalance> _balances;
    private readonly ITenantContext _tenant;

    public WalletBalanceDistributionReport(IReadOnlyRepository<UserBalance> balances, ITenantContext tenant)
    {
        _balances = balances; _tenant = tenant;
    }

    public Task<ReportDocument> GenerateAsync(object? parameters, CancellationToken cancellationToken = default)
        => GenerateAsync((parameters as DateRangeParameters) ?? new DateRangeParameters(), cancellationToken);

    public async Task<ReportDocument> GenerateAsync(DateRangeParameters parameters, CancellationToken cancellationToken = default)
    {
        // (cap, label) — ascending
        var buckets = new (decimal Max, string Label)[]
        {
            (0m,        "Empty (0)"),
            (50m,       "0.01 – 50"),
            (200m,      "50 – 200"),
            (500m,      "200 – 500"),
            (1000m,     "500 – 1,000"),
            (5000m,     "1,000 – 5,000"),
            (decimal.MaxValue, "5,000 +"),
        };

        var balances = await _balances.NoTrackingQuery()
            .Select(b => b.AvailableBalance)
            .ToListAsync(cancellationToken);

        var totals = balances.Sum();
        var rows = new List<IReadOnlyList<object?>>(buckets.Length);
        foreach (var (max, label) in buckets)
        {
            var count = balances.Count(v => v <= max && v > (label == "Empty (0)" ? -0.01m : PreviousMax(buckets, max)));
            var value = balances.Where(v => v <= max && v > (label == "Empty (0)" ? -0.01m : PreviousMax(buckets, max))).Sum();
            rows.Add(new object?[] { label, count, value, totals == 0 ? 0 : Math.Round(value / totals * 100, 1) });
        }

        return new ReportDocument
        {
            Title       = "Wallet balance distribution",
            Subtitle    = $"snapshot @ {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC",
            TenantName  = _tenant.ClientCode,
            Summary     = new[]
            {
                new ReportKpi("Total wallets",   balances.Count.ToString("N0")),
                new ReportKpi("Total balance ৳", $"৳ {totals:N0}"),
                new ReportKpi("Average ৳",       balances.Count == 0 ? "৳ 0" : $"৳ {totals/balances.Count:N0}")
            },
            Sections = new[]
            {
                new ReportSection
                {
                    Heading = "Balance buckets",
                    Columns = new[]
                    {
                        new ReportColumn("Bucket",   ReportColumnType.Text,   WidthPercent: 35),
                        new ReportColumn("Wallets",  ReportColumnType.Number, WidthPercent: 20, AlignRight: true),
                        new ReportColumn("Value ৳",  ReportColumnType.Money,  WidthPercent: 25, AlignRight: true),
                        new ReportColumn("Share %",  ReportColumnType.Number, WidthPercent: 20, AlignRight: true),
                    },
                    Rows = rows
                }
            }
        };
    }

    private static decimal PreviousMax((decimal Max, string Label)[] buckets, decimal currentMax)
    {
        decimal prev = 0;
        foreach (var (max, _) in buckets)
        {
            if (max == currentMax) return prev;
            prev = max;
        }
        return 0;
    }
}
