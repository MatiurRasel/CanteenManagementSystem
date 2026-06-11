// =============================================================================
// TopSpendersReport  (CanteenManagementSystem.Infrastructure.Reports.Reports)
// -----------------------------------------------------------------------------
// Per-user spend over a date window, sorted descending. Used to:
//   * spot heavy users who might benefit from a loyalty bump,
//   * audit unusually high spend (potential card mis-use),
//   * size top-up incentives ahead of a marketing push.
//
// PARAMETERS
//   DateRangeParameters (From, To) + Top (default 25, clamped 5–500).
//   User type filter optional — leave null to include everyone.
//
// COLUMNS
//   UserId | Name | UserType | OrderCount | OrderedQty | TotalSpend ৳
//
// ADR 0004: IReadOnlyRepository<Order>/<Student>/<Employee> — pure read.
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions.Reporting;
using Platform.Application.Persistence;
using Platform.Domain.Tenancy;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Orders;
using CanteenManagementSystem.Domain.Users;

namespace CanteenManagementSystem.Infrastructure.Reports.Reports;

public sealed class TopSpendersParameters : DateRangeParameters
{
    /// <summary>How many spenders to list. Clamped to [5, 500]. Default 25.</summary>
    public int Top { get; set; } = 25;

    /// <summary>Restrict to one user type (Student / Employee). Null = both.</summary>
    public string? UserType { get; set; }
}

public sealed class TopSpendersReport : IReport<TopSpendersParameters>
{
    public string Key                                   => "canteen-top-spenders";
    public string DisplayName                           => "Top spenders";
    public string? Description                          => "Highest-spending users in a date window — informs loyalty and audit.";
    public string Group                                 => "Wallet";
    public IReadOnlyList<string> RequiredPermissions    => new[] { "Reports.View" };
    public IReadOnlyList<ReportFormat> SupportedFormats => new[] { ReportFormat.Pdf, ReportFormat.Xlsx, ReportFormat.Csv, ReportFormat.Html, ReportFormat.Json };
    public Type ParameterType                           => typeof(TopSpendersParameters);

    private readonly IReadOnlyRepository<Order> _orders;
    private readonly IReadOnlyRepository<Student> _students;
    private readonly IReadOnlyRepository<Employee> _employees;
    private readonly ITenantContext _tenant;

    public TopSpendersReport(
        IReadOnlyRepository<Order> orders,
        IReadOnlyRepository<Student> students,
        IReadOnlyRepository<Employee> employees,
        ITenantContext tenant)
    {
        _orders = orders; _students = students; _employees = employees; _tenant = tenant;
    }

    public Task<ReportDocument> GenerateAsync(object? parameters, CancellationToken cancellationToken = default)
        => GenerateAsync((parameters as TopSpendersParameters) ?? new TopSpendersParameters(), cancellationToken);

    public async Task<ReportDocument> GenerateAsync(TopSpendersParameters parameters, CancellationToken cancellationToken = default)
    {
        var (from, to) = parameters.NormalizedUtc(defaultWindowDays: 30);
        var toExcl = to.AddDays(1);
        var top = Math.Clamp(parameters.Top, 5, 500);

        // Optional user-type filter.
        var typeFilter = Enum.TryParse<CanteenUserType>(parameters.UserType, ignoreCase: true, out var ut)
            ? (CanteenUserType?)ut : null;

        // Pull delivered-status orders in window, group by user, sum spend + count.
        var deliveredOnly = _orders.NoTrackingQuery()
            .Where(o => o.OrderDate >= from && o.OrderDate < toExcl
                     && o.Status == CanteenOrderStatus.Delivered);
        if (typeFilter is not null) deliveredOnly = deliveredOnly.Where(o => o.UserType == typeFilter);

        var spendByUser = await deliveredOnly
            .GroupBy(o => new { o.UserId, o.UserType })
            .Select(g => new
            {
                g.Key.UserId,
                g.Key.UserType,
                OrderCount  = g.Count(),
                OrderedQty  = g.SelectMany(o => o.OrderItems).Sum(oi => (int?)oi.Quantity) ?? 0,
                TotalSpend  = g.Sum(o => (decimal?)o.TotalAmount) ?? 0m
            })
            .OrderByDescending(r => r.TotalSpend)
            .Take(top)
            .ToListAsync(cancellationToken);

        // Batch-resolve display names.
        var studentIds  = spendByUser.Where(s => s.UserType == CanteenUserType.Student).Select(s => s.UserId).ToList();
        var employeeIds = spendByUser.Where(s => s.UserType == CanteenUserType.Employee).Select(s => s.UserId).ToList();

        var studentNames = studentIds.Count == 0 ? new Dictionary<string, string>() :
            (await _students.NoTrackingQuery()
                .Where(s => studentIds.Contains(s.ExternalId))
                .Select(s => new { s.ExternalId, s.Name })
                .ToListAsync(cancellationToken))
                .ToDictionary(x => x.ExternalId, x => x.Name);

        var employeeNames = employeeIds.Count == 0 ? new Dictionary<string, string>() :
            (await _employees.NoTrackingQuery()
                .Where(e => employeeIds.Contains(e.ExternalId))
                .Select(e => new { e.ExternalId, e.Name })
                .ToListAsync(cancellationToken))
                .ToDictionary(x => x.ExternalId, x => x.Name);

        var rows = spendByUser.Select(s => (IReadOnlyList<object?>)new object?[]
        {
            s.UserId,
            s.UserType == CanteenUserType.Student
                ? studentNames.GetValueOrDefault(s.UserId, "(unknown)")
                : employeeNames.GetValueOrDefault(s.UserId, "(unknown)"),
            s.UserType.ToString(),
            s.OrderCount,
            s.OrderedQty,
            s.TotalSpend
        }).ToList();

        var grandTotal = spendByUser.Sum(s => s.TotalSpend);

        return new ReportDocument
        {
            Title      = "Top spenders",
            Subtitle   = $"{from:yyyy-MM-dd} → {to:yyyy-MM-dd}" + (typeFilter is null ? "" : $" · {typeFilter}"),
            TenantName = _tenant.ClientCode,
            Summary    = new[]
            {
                new ReportKpi("Listed",  spendByUser.Count.ToString("N0"), $"top {top}"),
                new ReportKpi("Total ৳", $"৳ {grandTotal:N0}"),
                new ReportKpi("Average ৳ per spender", spendByUser.Count == 0 ? "৳ 0" : $"৳ {grandTotal / spendByUser.Count:N0}")
            },
            Sections = new[]
            {
                new ReportSection
                {
                    Heading = "Spenders",
                    Columns = new[]
                    {
                        new ReportColumn("User ID",     ReportColumnType.Text,   WidthPercent: 14),
                        new ReportColumn("Name",        ReportColumnType.Text,   WidthPercent: 28),
                        new ReportColumn("Type",        ReportColumnType.Text,   WidthPercent: 10),
                        new ReportColumn("Orders",      ReportColumnType.Number, WidthPercent: 12, AlignRight: true),
                        new ReportColumn("Items",       ReportColumnType.Number, WidthPercent: 12, AlignRight: true),
                        new ReportColumn("Total ৳",     ReportColumnType.Money,  WidthPercent: 24, AlignRight: true)
                    },
                    Rows = rows
                }
            }
        };
    }
}
