// =============================================================================
// ParentController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Per ADR 0004 the controller depends on IParentPortalService + IWalletService
// — no IAppDbContext.
// =============================================================================

using System.Security.Claims;
using CanteenManagementSystem.Application.Parents;
using CanteenManagementSystem.Domain.Cards;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Audit;
using Platform.Application.Abstractions.Wallets;
using Platform.Application.Persistence;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "AuthenticatedAny")]
[Route("parent")]
public sealed class ParentController : Controller
{
    private readonly IParentPortalService _portal;
    private readonly IWalletService _wallet;
    private readonly IUnitOfWork _uow;
    private readonly IAuditTrail _audit;

    public ParentController(
        IParentPortalService portal, IWalletService wallet,
        IUnitOfWork uow, IAuditTrail audit)
    {
        _portal = portal; _wallet = wallet; _uow = uow; _audit = audit;
    }

    public sealed record ChildVm(
        string ExternalId, string Name, string? Program, string? ContactNo,
        decimal Balance, decimal SpentThisMonth, int? ActiveCardId, string? ActiveCardUid,
        CardStatus? ActiveCardStatus,
        IReadOnlyList<Order> RecentOrders);

    public sealed record DashboardVm(IReadOnlyList<ChildVm> Children);

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var uid = ResolveUserId();
        if (uid is null) return View(new DashboardVm(Array.Empty<ChildVm>()));
        var linked = await _portal.GetLinkedChildIdsAsync(uid.Value, ct);
        var dash = await _portal.BuildDashboardAsync(linked, ct);
        return View(new DashboardVm(dash.Select(c => new ChildVm(
            c.ExternalId, c.Name, c.Program, c.ContactNo, c.Balance, c.SpentThisMonth,
            c.ActiveCardId, c.ActiveCardUid, c.ActiveCardStatus, c.RecentOrders)).ToList()));
    }

    public sealed class TopupForm
    {
        public string ExternalId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    [HttpPost("topup")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Topup(TopupForm form, CancellationToken ct)
    {
        if (form.Amount <= 0) { TempData["Flash.Error"] = "Amount must be positive."; return RedirectToAction(nameof(Index)); }
        var uid = ResolveUserId();
        if (uid is null) return RedirectToAction(nameof(Index));

        var allowed = await _portal.GetLinkedChildIdsAsync(uid.Value, ct);
        if (!allowed.Contains(form.ExternalId, StringComparer.Ordinal))
        {
            TempData["Flash.Error"] = "You are not linked to that child.";
            return RedirectToAction(nameof(Index));
        }
        var r = await _wallet.RechargeAsync(form.ExternalId, CanteenUserType.Student.ToString(),
            form.Amount, $"Parent top-up ({User.Identity?.Name})", ct);
        if (!r.IsSuccess) { TempData["Flash.Error"] = r.Error.Message; return RedirectToAction(nameof(Index)); }
        await _audit.RecordAsync("Wallet.ParentTopup", "Student", form.ExternalId, new { form.Amount }, ct);
        await _uow.SaveChangesAsync(ct);
        TempData["Flash.Success"] = $"৳ {form.Amount:N0} added to {form.ExternalId}'s wallet.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("freeze-card")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FreezeCard(int cardId, CancellationToken ct)
    {
        var uid = ResolveUserId();
        if (uid is null) return RedirectToAction(nameof(Index));
        var allowed = await _portal.GetLinkedChildIdsAsync(uid.Value, ct);
        var result = await _portal.FreezeChildCardAsync(cardId, allowed, User.Identity?.Name, ct);
        if (!result.IsSuccess)
        {
            if (result.Error.Code == "not_found")   return NotFound();
            if (result.Error.Code == "unauthorized") return Forbid();
            TempData["Flash.Warning"] = result.Error.Message;
            return RedirectToAction(nameof(Index));
        }
        TempData["Flash.Success"] = "Card frozen. Ask the canteen admin to re-issue if found.";
        return RedirectToAction(nameof(Index));
    }

    private int? ResolveUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var n) ? n : null;
    }
}
