// =============================================================================
// HomeController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// The platform's "root" controller. Two responsibilities:
//
//   1. /  → role-based smart landing.
//        * Anonymous            → /Account/Login
//        * Operator / Cashier   → /Verification  (counter scan)
//        * Kitchen              → /Kitchen
//        * Auditor              → /admin/audit
//        * TenantAdmin / SystemAdmin → /admin/dashboard
//        * Parent               → /parent
//        * Student / Employee   → /me/orders
//        * Anything else        → /admin/dashboard (safe default)
//
//   2. /Home/Error → RFC 7807-friendly error page (works without auth).
// =============================================================================

using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using CanteenManagementSystem.Presentation.Models;

namespace CanteenManagementSystem.Presentation.Controllers;

public sealed class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    [Route("")]
    [Route("Home")]
    [Route("Home/Index")]
    public IActionResult Index()
    {
        if (User?.Identity?.IsAuthenticated != true)
        {
            return Redirect("/Account/Login");
        }

        var landing = ResolveLanding(User);
        return Redirect(landing);
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
        => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    /// <summary>
    /// Pick the landing URL by role. Highest-privilege role wins so a SystemAdmin
    /// also holding an Operator role doesn't get bounced into the scan screen.
    /// </summary>
    private static string ResolveLanding(ClaimsPrincipal user)
    {
        if (user.IsInRole("SystemAdmin"))    return "/admin/dashboard";
        if (user.IsInRole("TenantAdmin"))    return "/admin/dashboard";
        if (user.IsInRole("Auditor"))        return "/admin/audit";
        if (user.IsInRole("Kitchen"))        return "/Kitchen";
        if (user.IsInRole("Operator"))       return "/Verification";
        if (user.IsInRole("Cashier"))        return "/Verification";
        if (user.IsInRole("Parent"))         return "/parent";
        if (user.IsInRole("Student"))        return "/me/orders";
        if (user.IsInRole("Employee"))       return "/me/orders";
        return "/admin/dashboard";
    }
}
