// =============================================================================
// RefundVoidLogReport
// -----------------------------------------------------------------------------
// Every refund or void in a date window, who performed it, the operator
// reason note, and the resulting wallet/inventory impact. Compliance + audit.
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

public sealed class RefundVoidLogReport : IReport<DateRangeParameters>
{
    public string Key                                   => "canteen-refund-void-log";
    public string DisplayName                           => "Refund / void log";
    public string? Description                          => "Every voided and refunded order in a date range.";
    public string Group                                 => "Audit";
    public IReadOnlyList<string> RequiredPermissions    => new[] { "Reports.View", "Audit.View" };
    public IReadOnlyList<ReportFormat> SupportedFormats => new[] { ReportFormat.Pdf, ReportFormat.Xlsx, ReportFormat.Csv, ReportFormat.Html, ReportFormat.Json };
    public Type ParameterType                           => typeof(DateRangeParameters);

    private readonly IReadOnlyRepository<Order> _orders;
    private readonly IReadOnlyRepository<WalletLedger> _ledger;
    private readonly ITenantContext _tenant;

    public RefundVoidLogReport(
        IReadOnlyRepository<Order> orders,
        IReadOnlyRepository<WalletLedger> ledger,
        ITenantContext tenant)
    {
        _orders = orders; _ledger = ledger; _tenant = tenant;
    }

    public Task<ReportDocument> GenerateAsync(object? parameters, CancellationToken cancellationToken = default)
        => GenerateAsync((parameters as DateRangeParameters) ?? new DateRangeParameters(), cancellationToken);

    public async Task<ReportDocument> GenerateAsync(DateRangeParameters parameters, CancellationToken cancellationToken = default)
    {
        var (from, to) = parameters.NormalizedUtc(defaultWindowDays: 30);
        var toExcl = to.AddDays(1);

        // Pull the ledger row that recorded the refund/void so we can surface
        // the operator's reason (the application records the reason on
        // WalletLedger.Reason; the order entity itself stores only status).
        var voids = await _orders.NoTrackingQuery()
            .Where(o => o.OrderDate >= from && o.OrderDate < toExcl)
            .Where(o => o.Status == CanteenOrderStatus.Cancelled || o.Status == CanteenOrderStatus.Refunded)
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new
            {
                o.OrderDate, o.OrderNumber, o.UserId, Status = o.Status, o.TotalAmount, o.OrderID
            })
            .ToListAsync(cancellationToken);

        var orderIds = voids.Select(v => v.OrderID).ToList();
        var reasons = await _ledger.NoTrackingQuery()
            .Where(l => l.OrderID != null && orderIds.Contains(l.OrderID!.Value))
            .Where(l => l.EntryType == WalletLedgerEntryType.Refund || l.EntryType == WalletLedgerEntryType.OrderRelease)
            .GroupBy(l => l.OrderID)
            .Select(g => new { OrderID = g.Key, Reason = g.OrderByDescending(l => l.CreatedAtUtc).Select(l => l.Reason).FirstOrDefault() })
            .ToListAsync(cancellationToken);
        var reasonByOrder = reasons.ToDictionary(r => r.OrderID!.Value, r => r.Reason ?? string.Empty);

        var rows = voids.Select(v => (IReadOnlyList<object?>)new object?[]
        {
            v.OrderDate, v.OrderNumber, v.UserId,
            v.Status == CanteenOrderStatus.Refunded ? "Refunded" : "Voided",
            v.TotalAmount,
            reasonByOrder.GetValueOrDefault(v.OrderID, string.Empty)
        }).ToList();

        var refundedValue = voids.Where(v => v.Status == CanteenOrderStatus.Refunded).Sum(v => v.TotalAmount);
        var voidedValue   = voids.Where(v => v.Status == CanteenOrderStatus.Cancelled).Sum(v => v.TotalAmount);

        return new ReportDocument
        {
            Title       = "Refund / void log",
            Subtitle    = $"{from:yyyy-MM-dd} → {to:yyyy-MM-dd}",
            TenantName  = _tenant.ClientCode,
            Summary     = new[]
            {
                new ReportKpi("Refunds ৳", $"৳ {refundedValue:N0}", $"{voids.Count(v => v.Status == CanteenOrderStatus.Refunded)} order(s)"),
                new ReportKpi("Voids ৳",   $"৳ {voidedValue:N0}",   $"{voids.Count(v => v.Status == CanteenOrderStatus.Cancelled)} order(s)")
            },
            Sections = new[]
            {
                new ReportSection
                {
                    Heading = "Per-order log",
                    Columns = new[]
                    {
                        new ReportColumn("Placed",      ReportColumnType.Date,   WidthPercent: 18),
                        new ReportColumn("Order #",     ReportColumnType.Text,   WidthPercent: 16),
                        new ReportColumn("User",        ReportColumnType.Text,   WidthPercent: 12),
                        new ReportColumn("Action",      ReportColumnType.Text,   WidthPercent: 10),
                        new ReportColumn("Amount ৳",    ReportColumnType.Money,  WidthPercent: 12, AlignRight: true),
                        new ReportColumn("Reason",      ReportColumnType.Text,   WidthPercent: 32),
                    },
                    Rows = rows
                }
            }
        };
    }
}
