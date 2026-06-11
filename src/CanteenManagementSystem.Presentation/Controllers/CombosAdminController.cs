// =============================================================================
// CombosAdminController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// CRUD for counter quick-buttons (combos / meal deals). Per ADR 0004 the
// controller only talks to IComboButtonService — no IAppDbContext.
//
//   GET  /admin/combos               — list with create button
//   GET  /admin/combos/edit/{id?}    — form (new + edit)
//   POST /admin/combos/save          — upsert
//   POST /admin/combos/delete/{id}   — delete
//
// The counter renders combos by calling GET /admin/combos/resolve (JSON).
// =============================================================================

using CanteenManagementSystem.Application.Menus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Audit;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "TenantAdmin")]
[Route("admin/combos")]
public sealed class CombosAdminController : Controller
{
    private readonly IComboButtonService _combos;
    private readonly IMenuManagementService _menus;
    private readonly IAuditTrail _audit;

    public CombosAdminController(IComboButtonService combos, IMenuManagementService menus, IAuditTrail audit)
    {
        _combos = combos; _menus = menus; _audit = audit;
    }

    public sealed record ComboFormVm(
        int? ComboButtonId, string Code, string DisplayName, string? Color,
        string FoodItemIdsCsv, int SortOrder, bool IsActive,
        IReadOnlyList<CanteenManagementSystem.Domain.Menu.FoodItem> AvailableFoodItems);

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _combos.ListAsync(includeInactive: true, ct));

    [HttpGet("edit/{id:int?}")]
    public async Task<IActionResult> Edit(int? id, CancellationToken ct)
    {
        var foods = await _menus.GetManageItemsAsync();
        if (id is null)
        {
            return View(new ComboFormVm(null, "", "", "#6f42c1", "", 0, true, foods));
        }
        var c = await _combos.GetAsync(id.Value, ct);
        if (c is null) return NotFound();
        return View(new ComboFormVm(c.ComboButtonId, c.Code, c.DisplayName, c.Color,
            c.FoodItemIdsCsv, c.SortOrder, c.IsActive, foods));
    }

    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int? comboButtonId, string code, string displayName,
        string? color, string foodItemIdsCsv, int sortOrder, bool isActive, CancellationToken ct)
    {
        var ids = (foodItemIdsCsv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var n) ? n : 0)
            .Where(n => n > 0)
            .ToList();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(displayName) || ids.Count == 0)
        {
            TempData["Flash.Error"] = "Code, display name, and at least one item are required.";
            return RedirectToAction(nameof(Edit), new { id = comboButtonId });
        }
        var input = new ComboButtonInput(code, displayName, color, ids, sortOrder, isActive);
        if (comboButtonId is null)
        {
            var created = await _combos.CreateAsync(input, ct);
            await _audit.RecordAsync("Combo.Created", "ComboButton", created.ComboButtonId.ToString(),
                new { created.Code, created.DisplayName }, ct);
            TempData["Flash.Success"] = $"Combo '{created.Code}' created.";
        }
        else
        {
            var ok = await _combos.UpdateAsync(comboButtonId.Value, input, ct);
            if (!ok) return NotFound();
            await _audit.RecordAsync("Combo.Updated", "ComboButton", comboButtonId.Value.ToString(),
                new { code, displayName }, ct);
            TempData["Flash.Success"] = $"Combo '{code}' saved.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var ok = await _combos.DeleteAsync(id, ct);
        if (ok)
        {
            await _audit.RecordAsync("Combo.Deleted", "ComboButton", id.ToString(), payload: null, ct);
            TempData["Flash.Success"] = "Combo deleted.";
        }
        else
        {
            TempData["Flash.Error"] = "Combo not found.";
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>JSON list of active combos for the counter UI to render quick buttons.</summary>
    [HttpGet("resolve")]
    [AllowAnonymous]    // counter pages run as Operator; this endpoint is read-only + tenant-scoped
    public async Task<IActionResult> Resolve(CancellationToken ct)
    {
        var resolved = await _combos.ResolveActiveAsync(ct);
        return Json(resolved);
    }
}
