// =============================================================================
// ReportsAdminController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Razor surface wrapping IReportingService. Renders:
//   * Daily collection chart (revenue per day, order count)
//   * Top items bar list
//   * Peak-hour heatmap (24 buckets)
//   * Department spend table
//
// EXPORT
//   /admin/reports/export/daily.csv?from=...&to=...
//   Streams the bytes returned by IReportingService.ExportDailyCollectionCsvAsync.
// =============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Reports;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "Auditor")]
[Route("admin/reports")]
public sealed class ReportsAdminController : Controller
{
    private readonly IReportingService _reports;
    public ReportsAdminController(IReportingService reports) => _reports = reports;

    public sealed record DashboardVm(
        DateTime From, DateTime To,
        DailyCollectionReport Collection,
        IReadOnlyList<PopularItemRow> Popular,
        IReadOnlyList<PeakHourRow> PeakToday,
        IReadOnlyList<DepartmentSpendRow> Departments);

    [HttpGet("")]
    public async Task<IActionResult> Index(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var f = (from ?? DateTime.UtcNow.Date.AddDays(-6)).Date;
        var t = (to ?? DateTime.UtcNow).Date;
        if (t < f) (f, t) = (t, f);

        var collection  = await _reports.GetDailyCollectionAsync(f, t, ct);
        var popular     = await _reports.GetPopularItemsAsync(f, t, 10, ct);
        var peak        = await _reports.GetPeakHoursAsync(DateTime.UtcNow.Date, ct);
        var departments = await _reports.GetDepartmentSpendAsync(f, t, ct);
        return View(new DashboardVm(f, t, collection, popular, peak, departments));
    }

    [HttpGet("export/daily.csv")]
    public async Task<IActionResult> ExportDaily(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var f = (from ?? DateTime.UtcNow.Date.AddDays(-30)).Date;
        var t = (to ?? DateTime.UtcNow).Date;
        var bytes = await _reports.ExportDailyCollectionCsvAsync(f, t, ct);
        return File(bytes, "text/csv", $"daily_collection_{f:yyyyMMdd}_{t:yyyyMMdd}.csv");
    }
}
