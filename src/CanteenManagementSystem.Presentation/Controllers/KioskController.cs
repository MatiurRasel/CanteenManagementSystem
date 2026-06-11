// =============================================================================
// KioskController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Self-service tablet surface. Two routes:
//
//   GET /kiosk                  → full-screen scan-and-order UI
//                                  Accepts either an Operator/Cashier cookie
//                                  OR a Kiosk-device cookie.
//   GET /kiosk/pair?token=…     → one-time device-binding endpoint. Validates
//                                  the raw token, signs a long-lived (30 day)
//                                  cookie with role "Kiosk" + the device's
//                                  tenant id, then redirects to /kiosk.
//
// After a device is paired ONCE (admin scans the QR / opens the deep-link
// on the tablet), the tablet just stays on /kiosk forever. No staff login,
// no special password, no operator credentials shown to students.
// =============================================================================

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Auth;
using Platform.Presentation.Auth;

namespace CanteenManagementSystem.Presentation.Controllers;

[Route("kiosk")]
public sealed class KioskController : Controller
{
    private readonly IKioskDeviceService _kiosks;

    public KioskController(IKioskDeviceService kiosks)
    {
        _kiosks = kiosks;
    }

    // ─── Main full-screen kiosk view (operator OR paired kiosk device) ────
    //
    // Marked [AllowAnonymous] so we can route unpaired tablets to the friendly
    // /kiosk/setup page instead of bouncing them to /Account/Login. We still
    // enforce role check inside — only authenticated Operator/Cashier/Kiosk
    // (or higher) see the live screen.
    [HttpGet("")]
    [AllowAnonymous]
    public IActionResult Index()
    {
        var u = User;
        var allowed = u?.Identity?.IsAuthenticated == true && (
            u.IsInRole("SystemAdmin") || u.IsInRole("TenantAdmin") ||
            u.IsInRole("Operator")    || u.IsInRole("Cashier") ||
            u.IsInRole("Kiosk"));
        if (!allowed) return RedirectToAction(nameof(Setup));
        return View();
    }

    // ─── Pairing endpoint — anonymous; the token IS the credential ────────
    [HttpGet("pair")]
    [AllowAnonymous]
    public async Task<IActionResult> Pair([FromQuery] string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["Flash.Error"] = "Missing pairing token. Ask your admin to generate one from /admin/kiosks.";
            return RedirectToAction(nameof(Setup));
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _kiosks.RedeemAsync(token.Trim(), ip, ct);
        if (result is null)
        {
            TempData["Flash.Error"] = "Invalid or revoked pairing token.";
            return RedirectToAction(nameof(Setup));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, $"kiosk:{result.KioskDeviceId}"),
            new(ClaimTypes.Name,           result.Name),
            new(ClaimTypes.Role,           "Kiosk"),
            new("kiosk_device_id",         result.KioskDeviceId.ToString()),
            new("client_id",               result.ClientId.ToString()),
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, PlatformAuthSchemes.Cookie));
        await HttpContext.SignInAsync(
            PlatformAuthSchemes.Cookie,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc   = DateTimeOffset.UtcNow.AddDays(30),
                // Devices stay paired across browser restarts — that's the point.
                AllowRefresh = true
            });

        return Redirect("/kiosk");
    }

    // ─── Anonymous setup page — shown when an unpaired tablet hits /kiosk ──
    [HttpGet("setup")]
    [AllowAnonymous]
    public IActionResult Setup() => View();
}
