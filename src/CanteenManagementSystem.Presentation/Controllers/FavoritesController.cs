// =============================================================================
// FavoritesController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Authenticated user manages their own list of favourite food items.
//   GET    /me/favorites           → JSON list
//   POST   /me/favorites/{foodId}  → add
//   DELETE /me/favorites/{id}      → remove
//
// "User identity" resolves to the linked person via IMeService (ResolveLinkedPersonIdAsync)
// so the same row is shared between /me/orders, /me/favorites, and re-order CTA.
// =============================================================================

using CanteenManagementSystem.Application.Favorites;
using CanteenManagementSystem.Application.Me;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize]
[Route("me/favorites")]
public sealed class FavoritesController : Controller
{
    private readonly IUserFavoritesService _favorites;
    private readonly IMeService _me;

    public FavoritesController(IUserFavoritesService favorites, IMeService me)
    {
        _favorites = favorites; _me = me;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var userId = await ResolveUserIdAsync(ct);

        // JSON callers (the existing /me/orders re-order button + fetch from kiosk)
        // still get 401 on no-link so they don't render stale data; HTML callers
        // get an empty-state page instead so a sysadmin browsing the menu finds
        // a friendly "no favourites yet, link your account first" view.
        if (userId is null)
        {
            if (Request.Headers.Accept.Any(h => h is not null && h.Contains("application/json")))
                return Unauthorized();
            return View(Array.Empty<CanteenManagementSystem.Application.Favorites.FavoriteItemDto>());
        }

        var items = await _favorites.ListAsync(userId, ct);

        if (Request.Headers.Accept.Any(h => h is not null && h.Contains("application/json")))
            return Json(items);

        return View(items);
    }

    [HttpPost("{foodItemId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int foodItemId, CancellationToken ct)
    {
        var userId = await ResolveUserIdAsync(ct);
        if (userId is null) return Unauthorized();
        var dto = await _favorites.AddAsync(userId, foodItemId, ct);
        return Json(new { success = true, favorite = dto });
    }

    [HttpDelete("{favoriteId:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(long favoriteId, CancellationToken ct)
    {
        var userId = await ResolveUserIdAsync(ct);
        if (userId is null) return Unauthorized();
        var ok = await _favorites.RemoveAsync(userId, favoriteId, ct);
        return Json(new { success = ok });
    }

    private async Task<string?> ResolveUserIdAsync(CancellationToken ct)
    {
        var loginUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(loginUserId, out var uid)) return null;
        return await _me.ResolveLinkedPersonIdAsync(uid, ct);
    }
}
