// =============================================================================
// ReportSchedulesController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// CRUD + "Run now" over ReportSchedules. Per ADR 0004 the controller depends
// on IReportScheduleAdminService — no IAppDbContext, no dispatcher/distributor.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Platform.Application.Abstractions.Reporting;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "TenantAdmin")]
[Route("admin/report-schedules")]
public sealed class ReportSchedulesController : Controller
{
    private readonly IReportScheduleAdminService _schedules;
    private readonly IReportRegistry _registry;

    public ReportSchedulesController(IReportScheduleAdminService schedules, IReportRegistry registry)
    {
        _schedules = schedules; _registry = registry;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _schedules.ListAsync(ct));

    public sealed class ScheduleForm
    {
        public int ScheduleId { get; set; }

        [Required, StringLength(64)]
        public string ReportKey { get; set; } = string.Empty;

        [Required, StringLength(128)]
        public string DisplayName { get; set; } = string.Empty;

        [Required] public string Recurrence { get; set; } = "Daily";

        [Range(0, 23)]  public int HourOfDay { get; set; } = 23;
        [Range(0, 59)]  public int Minute { get; set; } = 0;
        [Range(0, 6)]   public int? DayOfWeek { get; set; } = 1;
        [Range(1, 31)]  public int? DayOfMonth { get; set; } = 1;
        [Range(1, 24)]  public int? IntervalHours { get; set; } = 6;

        [Required] public string Format { get; set; } = "Pdf";

        [StringLength(2000)] public string? ParametersJson { get; set; } = "{}";

        [Required, StringLength(1000)]
        public string Recipients { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;
    }

    [HttpGet("new")]
    public IActionResult New()
    {
        ViewBag.Reports = ReportSelectList(null);
        return View("Edit", new ScheduleForm());
    }

    [HttpPost("new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> New(ScheduleForm form, CancellationToken ct)
    {
        if (!ValidateForm(form))
        {
            ViewBag.Reports = ReportSelectList(form.ReportKey);
            return View("Edit", form);
        }
        var result = await _schedules.CreateAsync(ToInput(form), ct);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error.Message ?? "Save failed.");
            ViewBag.Reports = ReportSelectList(form.ReportKey);
            return View("Edit", form);
        }
        TempData["Flash.Success"] = $"Schedule \"{result.Value!.DisplayName}\" created; first run at {result.Value.NextRunAtUtc:u}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var row = await _schedules.GetByIdAsync(id, ct);
        if (row is null) return NotFound();
        ViewBag.Reports = ReportSelectList(row.ReportKey);
        return View(new ScheduleForm
        {
            ScheduleId     = row.ScheduleId,
            ReportKey      = row.ReportKey,
            DisplayName    = row.DisplayName,
            Recurrence     = row.Recurrence,
            HourOfDay      = row.HourOfDay ?? 23,
            Minute         = row.Minute ?? 0,
            DayOfWeek      = row.DayOfWeek,
            DayOfMonth     = row.DayOfMonth,
            IntervalHours  = row.IntervalHours,
            Format         = row.Format,
            ParametersJson = row.ParametersJson,
            Recipients     = row.Recipients,
            IsEnabled      = row.IsEnabled
        });
    }

    [HttpPost("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ScheduleForm form, CancellationToken ct)
    {
        if (!ValidateForm(form))
        {
            ViewBag.Reports = ReportSelectList(form.ReportKey);
            return View(form);
        }
        var result = await _schedules.UpdateAsync(id, ToInput(form), ct);
        if (!result.IsSuccess) return NotFound();
        TempData["Flash.Success"] = "Schedule saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, CancellationToken ct)
    {
        var result = await _schedules.ToggleEnabledAsync(id, ct);
        if (!result.IsSuccess) return NotFound();
        TempData["Flash.Success"] = "Schedule toggled.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _schedules.DeleteAsync(id, ct);
        if (!result.IsSuccess) return NotFound();
        TempData["Flash.Warning"] = "Schedule deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/run-now")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunNow(int id, CancellationToken ct)
    {
        var outcome = await _schedules.RunNowAsync(id, ct);
        TempData[outcome.Success ? "Flash.Success" : "Flash.Error"] = outcome.Success
            ? $"Sent to {outcome.RecipientCount} recipient(s)."
            : "Run failed: " + (outcome.Detail ?? "(unknown)");
        return RedirectToAction(nameof(Index));
    }

    // ─── helpers ──────────────────────────────────────────────────────────

    private ReportScheduleInput ToInput(ScheduleForm f) => new(
        f.ReportKey, f.DisplayName, f.Recurrence,
        f.HourOfDay, f.Minute, f.DayOfWeek, f.DayOfMonth, f.IntervalHours,
        f.Format, f.ParametersJson, f.Recipients, f.IsEnabled,
        CreatedBy: User.Identity?.Name);

    private List<SelectListItem> ReportSelectList(string? selectedKey)
        => _registry.All().Select(r => new SelectListItem
        {
            Value = r.Key,
            Text  = $"{r.DisplayName}  ({r.Group} · {r.Key})",
            Selected = string.Equals(r.Key, selectedKey, StringComparison.OrdinalIgnoreCase)
        }).ToList();

    private bool ValidateForm(ScheduleForm form)
    {
        if (_registry.Find(form.ReportKey) is null)
        {
            ModelState.AddModelError(nameof(form.ReportKey), $"Unknown report '{form.ReportKey}'.");
            return false;
        }
        if (form.Recurrence is not ("Daily" or "Weekly" or "Monthly" or "Hourly"))
        {
            ModelState.AddModelError(nameof(form.Recurrence), "Must be Daily / Weekly / Monthly / Hourly.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(form.Recipients))
        {
            ModelState.AddModelError(nameof(form.Recipients), "At least one recipient email required.");
            return false;
        }
        return true;
    }
}
