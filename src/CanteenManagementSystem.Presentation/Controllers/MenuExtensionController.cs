// =============================================================================
// MenuExtensionController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// JSON endpoints powering the /Menu/Manage drag-reorder UI + copy-yesterday +
// schedule-next-week. The drag-reorder action POSTs the new order from the
// browser via SortableJS; copy-yesterday + schedule-next-week call into
// existing IMenuManagementService methods.
// =============================================================================

using CanteenManagementSystem.Application.Menus;
using CanteenManagementSystem.Application.Menus.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Persistence;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "TenantAdmin")]
[Route("admin/menu")]
public sealed class MenuExtensionController : Controller
{
    private readonly IMenuManagementService _menus;
    private readonly IUnitOfWork _uow;

    public MenuExtensionController(IMenuManagementService menus, IUnitOfWork uow)
    {
        _menus = menus; _uow = uow;
    }

    public sealed class ReorderRequest
    {
        public List<ReorderRow> Items { get; set; } = new();
        public sealed class ReorderRow { public int DailyMenuId { get; set; } public int DisplayOrder { get; set; } }
    }

    [HttpPost("reorder")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder([FromBody] ReorderRequest request, CancellationToken ct)
    {
        if (request.Items.Count == 0) return Json(new { success = true, updated = 0 });

        var ids = request.Items.Select(i => i.DailyMenuId).ToList();
        var menus = _uow.Repository<CanteenManagementSystem.Domain.Menu.DailyMenu>();
        var rows = await menus.Query()
            .Where(dm => ids.Contains(dm.DailyMenuID))
            .ToListAsync(ct);

        var orderById = request.Items.ToDictionary(i => i.DailyMenuId, i => i.DisplayOrder);
        foreach (var row in rows)
        {
            if (orderById.TryGetValue(row.DailyMenuID, out var ord))
                row.DisplayOrder = ord;
        }
        await _uow.SaveChangesAsync(ct);
        return Json(new { success = true, updated = rows.Count });
    }

    /// <summary>
    /// Copy yesterday's menu into today. Convenience wrapper over the existing
    /// IMenuManagementService.CopyMenuAsync(source, target, overwrite=false).
    /// </summary>
    [HttpPost("copy-yesterday")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CopyYesterday()
    {
        var today = DateTime.Today;
        var result = await _menus.CopyMenuAsync(new CopyMenuRequestDto
        {
            SourceDate = today.AddDays(-1),
            TargetDate = today,
            OverwriteExisting = false
        });
        TempData[result.Success ? "Flash.Success" : "Flash.Error"] = result.Message;
        return RedirectToAction("Manage", "Menu");
    }

    /// <summary>
    /// Apply weekly templates over the next 7 days. Wrapper over
    /// IMenuManagementService.ApplyWeeklyTemplateAsync(start, durationDays=7).
    /// </summary>
    [HttpPost("schedule-next-week")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ScheduleNextWeek()
    {
        var nextMonday = NextMonday(DateTime.Today);
        var result = await _menus.ApplyWeeklyTemplateAsync(new ApplyWeeklyTemplateRequestDto
        {
            StartDate = nextMonday,
            DurationDays = 7
        });
        TempData[result.Success ? "Flash.Success" : "Flash.Error"] = result.Message;
        return RedirectToAction("Manage", "Menu");
    }

    private static DateTime NextMonday(DateTime from)
    {
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)from.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0) daysUntilMonday = 7;
        return from.AddDays(daysUntilMonday);
    }
}
