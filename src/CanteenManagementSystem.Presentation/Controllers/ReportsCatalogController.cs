// =============================================================================
// ReportsCatalogController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// New unified reports surface backed by the IReportRegistry + IReportDispatcher.
// Lists every IReport registered in DI (canteen ships 3 to start) and lets the
// admin run any report with date-range parameters in any supported format.
//
// ROUTES
//   GET  /admin/reports-catalog                      list reports grouped
//   GET  /admin/reports-catalog/{key}                form page with format picker
//   GET  /admin/reports-catalog/{key}/download       run + return bytes
// =============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Reporting;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "Auditor")]
[Route("admin/reports-catalog")]
public sealed class ReportsCatalogController : Controller
{
    private readonly IReportRegistry _registry;
    private readonly IReportDispatcher _dispatcher;

    public ReportsCatalogController(IReportRegistry registry, IReportDispatcher dispatcher)
    {
        _registry = registry;
        _dispatcher = dispatcher;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        var groups = _registry.All()
            .GroupBy(r => r.Group, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return View(groups);
    }

    [HttpGet("{key}")]
    public IActionResult Run(string key)
    {
        var report = _registry.Find(key);
        if (report is null) return NotFound();
        ViewBag.Report = report;
        return View(report);
    }

    [HttpGet("{key}/download")]
    public async Task<IActionResult> Download(string key, string format = "Pdf", CancellationToken ct = default)
    {
        var report = _registry.Find(key);
        if (report is null) return NotFound();

        if (!Enum.TryParse<ReportFormat>(format, true, out var fmt))
            return BadRequest(new { error = $"Unknown format '{format}'." });

        // Echo Request.Query as form data — IReport params bind by name.
        var formData = Request.Query.ToDictionary(
            kv => kv.Key,
            kv => (string?)kv.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);

        try
        {
            var rendered = await _dispatcher.RunAsync(key, formData, fmt, ct);
            return File(rendered.Bytes, rendered.MimeType, rendered.SuggestedFileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
