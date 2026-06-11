// =============================================================================
// DiscountRulesController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Per ADR 0004 the controller depends on IDiscountRuleAdminService — no
// IAppDbContext. The controller still owns the form → JSON shaping
// (presentation-layer concern), then hands a fully-built DiscountRule to the
// service for persistence.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Pricing;
using Platform.Domain.Pricing;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "TenantAdmin")]
[Route("admin/discount-rules")]
public sealed class DiscountRulesController : Controller
{
    private readonly IDiscountRuleAdminService _rules;
    public DiscountRulesController(IDiscountRuleAdminService rules) => _rules = rules;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _rules.ListAsync(ct));

    public sealed class RuleForm
    {
        public int RuleId { get; set; }
        [Required, StringLength(64)] public string RuleCode { get; set; } = string.Empty;
        [Required, StringLength(200)] public string DisplayName { get; set; } = string.Empty;
        [StringLength(500)] public string? Description { get; set; }

        /// <summary>"DayOfWeekRole" | "ComboItems" | "Raw"</summary>
        public string ConditionTemplate { get; set; } = "Raw";
        public string? Day { get; set; }                   // for DayOfWeekRole
        public string? Roles { get; set; }                  // CSV
        public string? ComboFoodItemIds { get; set; }       // CSV of ints
        public string ConditionJson { get; set; } = "{}";

        /// <summary>"Percent" | "Flat" | "Raw"</summary>
        public string EffectTemplate { get; set; } = "Raw";
        public decimal? EffectAmount { get; set; }
        public string EffectJson { get; set; } = "{}";

        public int Priority { get; set; } = 100;
        public bool IsActive { get; set; } = true;
        public DateTime? ValidFromUtc { get; set; }
        public DateTime? ValidUntilUtc { get; set; }
    }

    [HttpGet("new")]
    public IActionResult New() => View("Edit", new RuleForm());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var row = await _rules.GetByIdAsync(id, ct);
        if (row is null) return NotFound();
        return View(new RuleForm
        {
            RuleId            = row.RuleId,
            RuleCode          = row.RuleCode,
            DisplayName       = row.DisplayName,
            Description       = row.Description,
            ConditionTemplate = "Raw", ConditionJson = row.ConditionJson,
            EffectTemplate    = "Raw", EffectJson    = row.EffectJson,
            Priority          = row.Priority,
            IsActive          = row.IsActive,
            ValidFromUtc      = row.ValidFromUtc,
            ValidUntilUtc     = row.ValidUntilUtc
        });
    }

    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(RuleForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Edit", form);

        // Form → JSON shaping is a presentation concern, kept here.
        var conditionJson = form.ConditionTemplate switch
        {
            "DayOfWeekRole" => BuildDayOfWeekRoleCondition(form),
            "ComboItems"    => BuildComboItemsCondition(form),
            _               => form.ConditionJson
        };
        var effectJson = form.EffectTemplate switch
        {
            "Percent" => $"{{\"type\":\"Percent\",\"amount\":{(form.EffectAmount ?? 0m).ToString(System.Globalization.CultureInfo.InvariantCulture)}}}",
            "Flat"    => $"{{\"type\":\"Flat\",\"amount\":{(form.EffectAmount ?? 0m).ToString(System.Globalization.CultureInfo.InvariantCulture)}}}",
            _         => form.EffectJson
        };

        var result = await _rules.SaveAsync(new DiscountRule
        {
            RuleId         = form.RuleId,
            RuleCode       = form.RuleCode.Trim(),
            DisplayName    = form.DisplayName.Trim(),
            Description    = form.Description,
            ConditionJson  = conditionJson,
            EffectJson     = effectJson,
            Priority       = form.Priority,
            IsActive       = form.IsActive,
            ValidFromUtc   = form.ValidFromUtc,
            ValidUntilUtc  = form.ValidUntilUtc,
        }, ct);

        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error.Message ?? "Save failed.");
            return View("Edit", form);
        }

        TempData["Flash.Success"] = $"Rule '{form.RuleCode}' saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, CancellationToken ct)
    {
        var result = await _rules.ToggleActiveAsync(id, ct);
        if (!result.IsSuccess) return NotFound();
        TempData["Flash.Success"] = "Rule toggled.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _rules.DeleteAsync(id, ct);
        if (!result.IsSuccess) return NotFound();
        TempData["Flash.Warning"] = "Rule removed.";
        return RedirectToAction(nameof(Index));
    }

    private static string BuildDayOfWeekRoleCondition(RuleForm f)
    {
        var roles = (f.Roles ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var rolesJson = string.Join(',', roles.Select(r => $"\"{r}\""));
        var day = (f.Day ?? "Tuesday");
        return $"{{\"type\":\"DayOfWeekRole\",\"day\":\"{day}\",\"roles\":[{rolesJson}]}}";
    }

    private static string BuildComboItemsCondition(RuleForm f)
    {
        var ids = (f.ComboFoodItemIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => int.TryParse(s, out _));
        return $"{{\"type\":\"ComboItems\",\"foodItemIds\":[{string.Join(',', ids)}]}}";
    }
}
