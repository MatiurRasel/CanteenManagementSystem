// =============================================================================
// KiosksAdminController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Tenant-admin surface for managing paired kiosk tablets.
//
//   GET  /admin/kiosks              → list of devices for this tenant
//   POST /admin/kiosks/issue        → mint a new device + raw token (shown ONCE)
//   POST /admin/kiosks/{id}/revoke  → disable a device
//
// Per ADR 0004 the controller depends only on IKioskDeviceService — no
// IAppDbContext, no repositories.
// =============================================================================

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Auth;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "TenantAdmin")]
[Route("admin/kiosks")]
public sealed class KiosksAdminController : Controller
{
    private readonly IKioskDeviceService _kiosks;

    public KiosksAdminController(IKioskDeviceService kiosks)
    {
        _kiosks = kiosks;
    }

    public sealed class IndexVm
    {
        public IReadOnlyList<KioskListItem> Devices { get; set; } = Array.Empty<KioskListItem>();
        public KioskIssueResult? JustIssued { get; set; }
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var devices = await _kiosks.ListAsync(ct);
        return View(new IndexVm { Devices = devices });
    }

    public sealed class IssueForm
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(120, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;
    }

    [HttpPost("issue")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Issue(IssueForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            var devices = await _kiosks.ListAsync(ct);
            return View(nameof(Index), new IndexVm { Devices = devices });
        }

        var raw = User.UserIdOrZero();
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var issued = await _kiosks.IssueAsync(form.Name, raw, baseUrl, ct);

        var devices2 = await _kiosks.ListAsync(ct);
        TempData["Flash.Success"] =
            $"Kiosk '{issued.Name}' paired. Open the pair URL below on the tablet ONCE — the token is not shown again.";
        return View(nameof(Index), new IndexVm { Devices = devices2, JustIssued = issued });
    }

    [HttpPost("{id:int}/revoke")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(int id, CancellationToken ct)
    {
        await _kiosks.RevokeAsync(id, ct);
        TempData["Flash.Warning"] = "Kiosk device revoked. The tablet will sign out on its next page load.";
        return RedirectToAction(nameof(Index));
    }
}

internal static class ClaimsPrincipalExtensions
{
    public static int UserIdOrZero(this System.Security.Claims.ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var n) ? n : 0;
    }
}
