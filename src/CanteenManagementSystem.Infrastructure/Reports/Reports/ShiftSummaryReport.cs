// =============================================================================
// ShiftSummaryReport  (CanteenManagementSystem.Infrastructure.Reports.Reports)
// -----------------------------------------------------------------------------
// End-of-shift cash + revenue breakdown for one operator on one date.
//   * Orders placed / delivered / refunded / voided counts
//   * Revenue collected (delivered) and refunded
//   * Top-up activity (cash in)
//   * Discrepancy column = collected - refunded - expected cash drop
//
// Used by the counter at shift-end (printable cash reconciliation) and by
// admins reviewing operator performance.
//
// ADR 0004: IReadOnlyRepository<Order> + IReadOnlyRepository<WalletLedger>.
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions.Reporting;
using Platform.Application.Persistence;
using Platform.Domain.Tenancy;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Orders;
using CanteenManagementSystem.Domain.Wallets;

namespace CanteenManagementSystem.Infrastructure.Reports.Reports;

public sealed class ShiftSummaryParameters
{
    /// <summary>Operator (User) account that worked the shift. Required.</summary>
    public string? OperatorUserName { get; set; }

    /// <summary>Calendar date of the shift; defaults to today (UTC).</summary>
    public DateTime? ShiftDate { get; set; }
}

public sealed class ShiftSummaryReport : IReport<ShiftSummaryParameters>
{
    public string Key                                   => "canteen-shift-summary";
    public string DisplayName                           => "Shift summary";
    public string? Description                          => "Per-operator daily cash + revenue reconciliation.";
    public string Group                                 => "Sales";
    public IReadOnlyList<string> RequiredPermissions    => new[] { "Reports.View" };
    public IReadOnlyList<ReportFormat> SupportedFormats => new[] { ReportFormat.Pdf, ReportFormat.Xlsx, ReportFormat.Csv, ReportFormat.Html, ReportFormat.Json };
    public Type ParameterType                           => typeof(ShiftSummaryParameters);

    private readonly IReadOnlyRepository<Order> _orders;
    private readonly IReadOnlyRepository<WalletLedger> _ledger;
    private readonly ITenantContext _tenant;

    public ShiftSummaryReport(
        IReadOnlyRepository<Order> orders,
        IReadOnlyRepository<WalletLedger> ledger,
        ITenantContext tenant)
    {
        _orders = orders; _ledger = ledger; _tenant = tenant;
    }

    public Task<ReportDocument> GenerateAsync(object? parameters, CancellationToken cancellationToken = default)
        => GenerateAsync((parameters as ShiftSummaryParameters) ?? new ShiftSummaryParameters(), cancellationToken);

    public async Task<ReportDocument> GenerateAsync(ShiftSummaryParameters parameters, CancellationToken cancellationToken = default)
    {
        var op = parameters.OperatorUserName?.Trim() ?? "(all operators)";
        var day = (parameters.ShiftDate ?? DateTime.UtcNow.Date).Date;
        var next = day.AddDays(1);

        var orders = _orders.NoTrackingQuery()
            .Where(o => o.OrderDate >= day && o.OrderDate < next);

        if (!string.IsNullOrWhiteSpace(parameters.OperatorUserName))
        {
            var u = parameters.OperatorUserName!.Trim();
            orders = orders.Where(o => o.UserId == u);
        }

        var placed = await orders.CountAsync(cancellationToken);
        var delivered = await orders.Where(o => o.Status == CanteenOrderStatus.Delivered).CountAsync(cancellationToken);
        var refunded = await orders.Where(o => o.Status == CanteenOrderStatus.Refunded).CountAsync(cancellationToken);
        var voided = await orders.Where(o => o.Status == CanteenOrderStatus.Cancelled).CountAsync(cancellationToken);

        var revenue = await orders.Where(o => o.Status == CanteenOrderStatus.Delivered).SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m;
        var refundsValue = await orders.Where(o => o.Status == CanteenOrderStatus.Refunded).SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m;

        // Top-ups recorded as wallet ledger entries on this date for this tenant.
        var topUps = await _ledger.NoTrackingQuery()
            .Where(l => l.CreatedAtUtc >= day && l.CreatedAtUtc < next && l.EntryType == WalletLedgerEntryType.Recharge)
            .SumAsync(l => (decimal?)l.Amount, cancellationToken) ?? 0m;

        var net = revenue - refundsValue;
        var rows = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Orders placed",    placed },
            new object?[] { "Orders delivered", delivered },
            new object?[] { "Orders refunded",  refunded },
            new object?[] { "Orders voided",    voided },
            new object?[] { "Revenue (delivered) ৳", revenue },
            new object?[] { "Refunds ৳",        refundsValue },
            new object?[] { "Net revenue ৳",    net },
            new object?[] { "Top-ups ৳",        topUps },
            new object?[] { "—— Signature ——",  "______________________" },
            new object?[] { "—— Closing cash ৳",  "______________________" },
        };

        return new ReportDocument
        {
            Title       = "Shift summary",
            Subtitle    = $"{op}  •  {day:yyyy-MM-dd}",
            TenantName  = _tenant.ClientCode,
            Summary     = new[]
            {
                new ReportKpi("Net revenue", $"৳ {net:N0}", $"{delivered} delivered"),
                new ReportKpi("Refunded",    $"৳ {refundsValue:N0}", $"{refunded} order(s)"),
                new ReportKpi("Top-ups",     $"৳ {topUps:N0}")
            },
            Sections = new[]
            {
                new ReportSection
                {
                    Heading = "Counter activity",
                    Columns = new[]
                    {
                        new ReportColumn("Metric", ReportColumnType.Text,   WidthPercent: 60),
                        new ReportColumn("Value",  ReportColumnType.Number, WidthPercent: 40, AlignRight: true)
                    },
                    Rows = rows
                }
            }
        };
    }
}
