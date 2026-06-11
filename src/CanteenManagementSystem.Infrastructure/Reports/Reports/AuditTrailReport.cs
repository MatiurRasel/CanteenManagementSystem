// =============================================================================
// AuditTrailReport  (CanteenManagementSystem.Infrastructure.Reports.Reports)
// -----------------------------------------------------------------------------
// Last N audit entries for the tenant. Filterable by date range + optional
// action prefix. Useful for compliance exports.
//
// ADR 0004: IReadOnlyRepository<AuditEntry> — pure read.
// =============================================================================

using Platform.Application.Abstractions.Reporting;
using Platform.Application.Persistence;
using Platform.Domain.Audit;
using Platform.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace CanteenManagementSystem.Infrastructure.Reports.Reports;

public sealed class AuditTrailParameters : DateRangeParameters
{
    public string? Action { get; set; }
    public int Take { get; set; } = 500;
}

public sealed class AuditTrailReport : IReport<AuditTrailParameters>
{
    public string Key                                  => "platform-audit-trail";
    public string DisplayName                          => "Audit trail";
    public string? Description                         => "Sensitive-action audit entries.";
    public string Group                                => "Compliance";
    public IReadOnlyList<string> RequiredPermissions   => new[] { "Audit.View" };
    public IReadOnlyList<ReportFormat> SupportedFormats => new[] { ReportFormat.Pdf, ReportFormat.Xlsx, ReportFormat.Csv, ReportFormat.Html, ReportFormat.Json };
    public Type ParameterType                           => typeof(AuditTrailParameters);

    private readonly IReadOnlyRepository<AuditEntry> _entries;
    private readonly ITenantContext _tenant;

    public AuditTrailReport(IReadOnlyRepository<AuditEntry> entries, ITenantContext tenant)
    {
        _entries = entries; _tenant = tenant;
    }

    public Task<ReportDocument> GenerateAsync(object? parameters, CancellationToken cancellationToken = default)
        => GenerateAsync((parameters as AuditTrailParameters) ?? new AuditTrailParameters(), cancellationToken);

    public async Task<ReportDocument> GenerateAsync(AuditTrailParameters parameters, CancellationToken cancellationToken = default)
    {
        var (from, to) = parameters.NormalizedUtc(defaultWindowDays: 30);
        var take = Math.Clamp(parameters.Take, 50, 5000);

        var qry = _entries.NoTrackingQuery()
            .Where(a => a.OccurredAtUtc >= from && a.OccurredAtUtc < to.AddDays(1));
        if (!string.IsNullOrWhiteSpace(parameters.Action))
            qry = qry.Where(a => a.Action.StartsWith(parameters.Action!));

        var entries = await qry.OrderByDescending(a => a.OccurredAtUtc).Take(take).ToListAsync(cancellationToken);

        var rows = entries.Select(a => (IReadOnlyList<object?>)new object?[]
        {
            a.OccurredAtUtc, a.Action, $"{a.EntityType ?? "—"}{(string.IsNullOrEmpty(a.EntityId) ? "" : ":" + a.EntityId)}",
            a.PerformedBy ?? "—", a.PerformedByRole ?? "", a.IpAddress ?? "", a.CorrelationId ?? ""
        }).ToList();

        return new ReportDocument
        {
            Title      = "Audit trail",
            Subtitle   = $"{from:yyyy-MM-dd} → {to:yyyy-MM-dd}" + (string.IsNullOrWhiteSpace(parameters.Action) ? "" : $" · action starts with '{parameters.Action}'"),
            TenantName = _tenant.ClientCode,
            Summary    = new[]
            {
                new ReportKpi("Entries", entries.Count.ToString()),
                new ReportKpi("Earliest", entries.LastOrDefault()?.OccurredAtUtc.ToString("u") ?? "—"),
                new ReportKpi("Latest",   entries.FirstOrDefault()?.OccurredAtUtc.ToString("u") ?? "—")
            },
            Sections = new[] { new ReportSection
            {
                Heading = "Entries",
                Columns = new[]
                {
                    new ReportColumn("When",        ReportColumnType.DateTime, WidthPercent: 18),
                    new ReportColumn("Action",      ReportColumnType.Text,     WidthPercent: 20),
                    new ReportColumn("Entity",      ReportColumnType.Text,     WidthPercent: 18),
                    new ReportColumn("By",          ReportColumnType.Text,     WidthPercent: 12),
                    new ReportColumn("Role",        ReportColumnType.Text,     WidthPercent: 10),
                    new ReportColumn("IP",          ReportColumnType.Text,     WidthPercent: 10),
                    new ReportColumn("Correlation", ReportColumnType.Text,     WidthPercent: 12)
                },
                Rows = rows
            }}
        };
    }
}
