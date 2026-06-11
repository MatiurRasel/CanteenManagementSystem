// =============================================================================
// ReconciliationReport  (CanteenManagementSystem.Infrastructure.Reports.Reports)
// -----------------------------------------------------------------------------
// Reconciles successful gateway payments against the wallet-ledger Recharge
// rows they should have generated. Surfaces mismatches so finance can chase
// any payment that succeeded at the gateway but didn't post to the wallet.
//
// COLUMNS
//   Method | Gateway PaymentRef | Amount | Settled (gateway) | Recharged (wallet) | Variance ৳
//
// SUCCESS DEFINITION
//   PaymentTransaction.Status == Succeeded
//   AND a WalletLedger row exists with EntryType == Recharge and
//       Reason == "{method} #{ref}"  (the source string the orchestrator writes)
//
// ADR 0004: IReadOnlyRepository — pure read.
// =============================================================================

using CanteenManagementSystem.Domain.Wallets;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions.Reporting;
using Platform.Application.Persistence;
using Platform.Domain.Payments;
using Platform.Domain.Tenancy;

namespace CanteenManagementSystem.Infrastructure.Reports.Reports;

public sealed class ReconciliationReport : IReport<DateRangeParameters>
{
    public string Key                                   => "canteen-payments-reconciliation";
    public string DisplayName                           => "Payments reconciliation";
    public string? Description                          => "Cross-checks gateway-settled payments against wallet recharges; flags variance.";
    public string Group                                 => "Wallet";
    public IReadOnlyList<string> RequiredPermissions    => new[] { "Reports.View", "Audit.View" };
    public IReadOnlyList<ReportFormat> SupportedFormats => new[] { ReportFormat.Pdf, ReportFormat.Xlsx, ReportFormat.Csv, ReportFormat.Html, ReportFormat.Json };
    public Type ParameterType                           => typeof(DateRangeParameters);

    private readonly IReadOnlyRepository<PaymentTransaction> _payments;
    private readonly IReadOnlyRepository<WalletLedger> _ledger;
    private readonly ITenantContext _tenant;

    public ReconciliationReport(
        IReadOnlyRepository<PaymentTransaction> payments,
        IReadOnlyRepository<WalletLedger> ledger,
        ITenantContext tenant)
    {
        _payments = payments; _ledger = ledger; _tenant = tenant;
    }

    public Task<ReportDocument> GenerateAsync(object? parameters, CancellationToken cancellationToken = default)
        => GenerateAsync((parameters as DateRangeParameters) ?? new DateRangeParameters(), cancellationToken);

    public async Task<ReportDocument> GenerateAsync(DateRangeParameters parameters, CancellationToken cancellationToken = default)
    {
        var (from, to) = parameters.NormalizedUtc(defaultWindowDays: 7);
        var toExcl = to.AddDays(1);

        var settled = await _payments.NoTrackingQuery()
            .Where(p => p.CreatedAtUtc >= from && p.CreatedAtUtc < toExcl && p.Status == PaymentStatus.Succeeded)
            .Select(p => new
            {
                Method = p.Method.ToString(),
                p.TransactionRef,
                p.Amount,
                p.CompletedAtUtc
            })
            .ToListAsync(cancellationToken);

        if (settled.Count == 0)
        {
            return new ReportDocument
            {
                Title = "Payments reconciliation",
                Subtitle = $"{from:yyyy-MM-dd} → {to:yyyy-MM-dd}",
                TenantName = _tenant.ClientCode,
                Summary = new[] { new ReportKpi("Settled payments", "0") },
                Sections = new[] { new ReportSection { Heading = "Per payment", Columns = BuildColumns(), Rows = new List<IReadOnlyList<object?>>() } }
            };
        }

        // Pull the matching ledger rows in one round trip.
        var refs = settled.Select(s => s.TransactionRef).ToList();
        var recharges = await _ledger.NoTrackingQuery()
            .Where(l => l.EntryType == WalletLedgerEntryType.Recharge
                     && l.Reason != null
                     && refs.Any(r => l.Reason!.Contains(r)))
            .Select(l => new { l.Reason, l.Amount })
            .ToListAsync(cancellationToken);

        var rechargeByRef = recharges
            .GroupBy(r => r.Reason!.Substring(r.Reason!.IndexOf('#') + 1).Trim())
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var rows = new List<IReadOnlyList<object?>>();
        decimal totalSettled = 0m, totalRecharged = 0m, totalVariance = 0m;
        int variant = 0;
        foreach (var s in settled)
        {
            var recharged = rechargeByRef.TryGetValue(s.TransactionRef, out var amt) ? amt : 0m;
            var variance = s.Amount - recharged;
            if (variance != 0m) variant++;
            totalSettled += s.Amount;
            totalRecharged += recharged;
            totalVariance += variance;
            rows.Add(new object?[]
            {
                s.Method,
                s.TransactionRef,
                s.Amount,
                s.CompletedAtUtc,
                recharged,
                variance
            });
        }

        return new ReportDocument
        {
            Title       = "Payments reconciliation",
            Subtitle    = $"{from:yyyy-MM-dd} → {to:yyyy-MM-dd}",
            TenantName  = _tenant.ClientCode,
            Summary = new[]
            {
                new ReportKpi("Settled payments", settled.Count.ToString("N0"), $"৳ {totalSettled:N0}"),
                new ReportKpi("Wallet recharges", $"৳ {totalRecharged:N0}"),
                new ReportKpi("Variance",         $"৳ {totalVariance:N0}", variant == 0 ? "all matched" : $"{variant} unmatched")
            },
            Sections = new[]
            {
                new ReportSection { Heading = "Per payment", Columns = BuildColumns(), Rows = rows }
            }
        };
    }

    private static ReportColumn[] BuildColumns() => new[]
    {
        new ReportColumn("Method",           ReportColumnType.Text,     WidthPercent: 12),
        new ReportColumn("Transaction Ref",  ReportColumnType.Text,     WidthPercent: 22),
        new ReportColumn("Gateway ৳",        ReportColumnType.Money,    WidthPercent: 14, AlignRight: true),
        new ReportColumn("Settled at",       ReportColumnType.DateTime, WidthPercent: 18),
        new ReportColumn("Wallet ৳",         ReportColumnType.Money,    WidthPercent: 14, AlignRight: true),
        new ReportColumn("Variance ৳",       ReportColumnType.Money,    WidthPercent: 20, AlignRight: true)
    };
}
