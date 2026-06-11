// =============================================================================
// BrandingAdminController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Tenant-admin surface for setting per-tenant branding overrides. Writes to
// TenantSettings keys "Branding.AccentColor", "Branding.AppName" etc. that
// IBrandingResolver layers ABOVE appsettings.json defaults.
//
// Effective precedence at runtime:
//   1. Per-user customizer choice (localStorage, applied client-side)   ← top
//   2. Tenant override            (this admin page writes these)
//   3. appsettings.json default
//   4. Hard-coded default in BrandingConfiguration
//
// Per ADR 0004 we depend on IBrandingResolver + ITenantSettings only —
// no IAppDbContext.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Branding;
using Platform.Application.Abstractions.Configuration;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "TenantAdmin")]
[Route("admin/branding")]
public sealed class BrandingAdminController : Controller
{
    private readonly IBrandingResolver _resolver;
    private readonly ITenantSettings _settings;

    public BrandingAdminController(IBrandingResolver resolver, ITenantSettings settings)
    {
        _resolver = resolver;
        _settings = settings;
    }

    public sealed class BrandingForm
    {
        [StringLength(120)] public string? AppName { get; set; }
        [StringLength(160)] public string? AppSubtitle { get; set; }
        [StringLength(20)]  public string? AccentColor { get; set; }
        [StringLength(500)] public string? LogoUrl { get; set; }
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var b = await _resolver.ResolveAsync(ct);
        return View(new BrandingForm
        {
            AppName     = b.AppName,
            AppSubtitle = b.AppSubtitle,
            AccentColor = b.AccentColor,
            LogoUrl     = b.LogoUrl,
        });
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(BrandingForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(nameof(Index), form);

        // Trim + persist. Empty string → write null so the next resolver pass
        // falls through to the appsettings default.
        async Task Persist(string key, string? value) =>
            await _settings.SetAsync($"Branding.{key}", string.IsNullOrWhiteSpace(value) ? null : value.Trim(), cancellationToken: ct);

        await Persist(nameof(BrandingForm.AppName),     form.AppName);
        await Persist(nameof(BrandingForm.AppSubtitle), form.AppSubtitle);
        await Persist(nameof(BrandingForm.AccentColor), form.AccentColor);
        await Persist("LogoUrl",                        form.LogoUrl);

        _resolver.Invalidate();

        TempData["Flash.Success"] = "Branding updated. Refresh other open tabs to see the new colour everywhere.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("reset")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reset(CancellationToken ct)
    {
        await _settings.SetAsync("Branding.AppName",     null, cancellationToken: ct);
        await _settings.SetAsync("Branding.AppSubtitle", null, cancellationToken: ct);
        await _settings.SetAsync("Branding.AccentColor", null, cancellationToken: ct);
        await _settings.SetAsync("Branding.LogoUrl",     null, cancellationToken: ct);
        _resolver.Invalidate();
        TempData["Flash.Warning"] = "Branding reset to platform defaults.";
        return RedirectToAction(nameof(Index));
    }
}
