// =============================================================================
// FeatureFlagsAdminController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Tenant-admin toggle screen for every well-known feature flag. Custom flags
// stay editable via /admin/settings (raw key/value editor).
// =============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Audit;
using Platform.Application.Abstractions.Configuration;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "TenantAdmin")]
[Route("admin/feature-flags")]
public sealed class FeatureFlagsAdminController : Controller
{
    private readonly IFeatureFlagService _flags;
    private readonly IAuditTrail _audit;

    public FeatureFlagsAdminController(IFeatureFlagService flags, IAuditTrail audit)
    {
        _flags = flags; _audit = audit;
    }

    public sealed record FeatureFlagsVm(IReadOnlyList<FeatureFlagRow> Flags);
    public sealed record FeatureFlagRow(string Key, string DisplayName, bool Enabled);

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var flags = await _flags.GetWellKnownAsync(ct);
        var rows = flags
            .OrderBy(kv => kv.Key)
            .Select(kv => new FeatureFlagRow(kv.Key, Humanise(kv.Key), kv.Value))
            .ToList();
        return View(new FeatureFlagsVm(rows));
    }

    [HttpPost("toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(string key, bool enabled, CancellationToken ct)
    {
        if (!FeatureFlags.All.Contains(key))
        {
            TempData["Flash.Error"] = $"Unknown feature flag '{key}'.";
            return RedirectToAction(nameof(Index));
        }
        await _flags.SetAsync(key, enabled, ct);
        await _audit.RecordAsync(
            action: "FeatureFlag.Toggled",
            entityType: "FeatureFlag",
            entityId: key,
            payload: new { enabled },
            cancellationToken: ct);
        TempData["Flash.Success"] = $"{Humanise(key)} → {(enabled ? "On" : "Off")}";
        return RedirectToAction(nameof(Index));
    }

    private static string Humanise(string key)
        => key.StartsWith("Feature.") ? System.Text.RegularExpressions.Regex.Replace(key.Substring(8), "([a-z])([A-Z])", "$1 $2") : key;
}
