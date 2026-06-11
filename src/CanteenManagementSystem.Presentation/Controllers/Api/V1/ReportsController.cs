using Asp.Versioning;
using Platform.Application.Abstractions.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace CanteenManagementSystem.Presentation.Controllers.Api.V1;

/// <summary>Operator + admin reporting endpoints. All responses are output-cached for 30s.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reports")]
[Produces("application/json")]
[OutputCache(PolicyName = "Reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportingService _reports;

    public ReportsController(IReportingService reports) => _reports = reports;

    /// <summary>Daily revenue + order count between two dates.</summary>
    [HttpGet("daily-collection")]
    public async Task<ActionResult<DailyCollectionReport>> DailyCollection([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken cancellationToken)
        => Ok(await _reports.GetDailyCollectionAsync(from, to, cancellationToken));

    /// <summary>Top-N popular items by quantity sold.</summary>
    [HttpGet("popular-items")]
    public async Task<ActionResult<IReadOnlyList<PopularItemRow>>> Popular([FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] int top = 10, CancellationToken cancellationToken = default)
        => Ok(await _reports.GetPopularItemsAsync(from, to, top, cancellationToken));

    /// <summary>Wastage = InitialQuantity - Sold, valued at unit price.</summary>
    [HttpGet("wastage")]
    public async Task<ActionResult<WastageReport>> Wastage([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken cancellationToken)
        => Ok(await _reports.GetWastageAsync(from, to, cancellationToken));

    /// <summary>Hour-of-day distribution for a single date.</summary>
    [HttpGet("peak-hours")]
    public async Task<ActionResult<IReadOnlyList<PeakHourRow>>> PeakHours([FromQuery] DateTime date, CancellationToken cancellationToken)
        => Ok(await _reports.GetPeakHoursAsync(date, cancellationToken));

    /// <summary>Department / programme spend rollup for corporate / educational tenants.</summary>
    [HttpGet("department-spend")]
    public async Task<ActionResult<IReadOnlyList<DepartmentSpendRow>>> DeptSpend([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken cancellationToken)
        => Ok(await _reports.GetDepartmentSpendAsync(from, to, cancellationToken));

    /// <summary>Same data as /daily-collection but streamed as a CSV file.</summary>
    [HttpGet("daily-collection.csv")]
    [Produces("text/csv")]
    public async Task<IActionResult> DailyCollectionCsv([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken cancellationToken)
    {
        var bytes = await _reports.ExportDailyCollectionCsvAsync(from, to, cancellationToken);
        return File(bytes, "text/csv", $"daily-collection-{from:yyyyMMdd}-{to:yyyyMMdd}.csv");
    }
}
