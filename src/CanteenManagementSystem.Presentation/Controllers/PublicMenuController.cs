// =============================================================================
// PublicMenuController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// GET /menu — anonymous, mobile-friendly preview of today's menu so a student
// can decide what to buy BEFORE walking to the counter. Same data source as
// /Display/Menu (the TV view) but laid out as a single-column scroll for a
// phone screen.
//
// Auth: AllowAnonymous so a student can pull it up via a QR-code printed on
// the noticeboard or shared via WhatsApp without signing in.
// =============================================================================

using CanteenManagementSystem.Application.Menus;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Presentation.Controllers;

[AllowAnonymous]
[Route("menu")]
public sealed class PublicMenuController : Controller
{
    private readonly IPublicMenuService _menu;

    public PublicMenuController(IPublicMenuService menu) => _menu = menu;

    [HttpGet("")]
    [ResponseCache(Duration = 30, Location = ResponseCacheLocation.Any, VaryByQueryKeys = new[] { "date" })]
    public async Task<IActionResult> Index(DateTime? date, CancellationToken ct)
    {
        var target = (date ?? DateTime.Today).Date;
        var items  = await _menu.GetTodayMenuAsync(target, ct);
        ViewBag.TargetDate = target;
        return View(items);
    }
}
