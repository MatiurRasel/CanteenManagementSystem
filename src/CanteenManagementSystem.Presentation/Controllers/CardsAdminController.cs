// =============================================================================
// CardsAdminController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Per ADR 0004 the controller depends on ICardAdminService — no IAppDbContext.
// All persistence + audit happens inside the service; the controller just
// shapes form input and routes the user.
// =============================================================================

using CanteenManagementSystem.Application.Cards;
using CanteenManagementSystem.Domain.Cards;
using CanteenManagementSystem.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Branding;
using Platform.Application.Abstractions.Users;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "TenantAdmin")]
[Route("admin/cards")]
public sealed class CardsAdminController : Controller
{
    private readonly ICardAdminService _cards;
    private readonly IUserDirectory _userDirectory;
    private readonly IBrandingResolver _branding;

    public CardsAdminController(
        ICardAdminService cards,
        IUserDirectory userDirectory,
        IBrandingResolver branding)
    {
        _cards = cards; _userDirectory = userDirectory; _branding = branding;
    }

    public sealed record CardRow(int CardId, string CardUid, string UserId, CanteenUserType UserType,
        CardStatus Status, DateTime IssuedAtUtc, DateTime? BlockedAtUtc);

    [HttpGet("")]
    public async Task<IActionResult> Index(string? q = null, CardStatus? status = null, CancellationToken ct = default)
    {
        var items = await _cards.ListAsync(q, status, ct);
        ViewBag.Q = q; ViewBag.Status = status;
        // Map application DTO → controller view-record so the existing Razor view binds unchanged.
        return View(items.Select(i =>
            new CardRow(i.CardId, i.CardUid, i.UserId, i.UserType, i.Status, i.IssuedAtUtc, i.BlockedAtUtc)).ToList());
    }

    public sealed record DetailVm(NfcCard Card, IReadOnlyList<CardEvent> Events);

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct)
    {
        var d = await _cards.GetDetailAsync(id, ct);
        return d is null ? NotFound() : View(new DetailVm(d.Card, d.Events));
    }

    public sealed record CardPrintVm(
        NfcCard Card, string UserName, string? UserPhotoUrl,
        string TenantName, string? TenantLogoUrl, string AccentColor);

    /// <summary>
    /// Print-friendly card template. Designed for credit-card-sized (CR80, 86×54mm)
    /// blank cards on a thermal printer or via browser Ctrl+P. Wears the tenant
    /// branding (logo + accent stripe) and the user's photo + display name.
    /// </summary>
    [HttpGet("{id:int}/print")]
    public async Task<IActionResult> Print(int id, CancellationToken ct)
    {
        var d = await _cards.GetDetailAsync(id, ct);
        if (d is null) return NotFound();

        var profile = await _userDirectory.FindByIdentifierAsync(d.Card.UserId, ct);
        var branding = await _branding.ResolveAsync(ct);
        return View(new CardPrintVm(
            d.Card,
            profile?.UserName ?? d.Card.UserId,
            profile?.PhotoUrl,
            branding.AppName,
            branding.LogoUrl,
            branding.AccentColor));
    }

    public sealed class IssueForm
    {
        public string CardUid { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public CanteenUserType UserType { get; set; } = CanteenUserType.Student;
        public string? Notes { get; set; }
    }

    [HttpGet("issue")]
    public IActionResult Issue() => View(new IssueForm());

    [HttpPost("issue")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Issue(IssueForm form, CancellationToken ct)
    {
        var result = await _cards.IssueAsync(
            new CardIssueInput(form.CardUid, form.UserId, form.UserType, form.Notes),
            performedBy: User.Identity?.Name, ct);

        if (!result.IsSuccess)
        {
            TempData["Flash.Error"] = result.Error.Message ?? "Could not issue card.";
            return View(form);
        }
        var card = result.Value!;
        TempData["Flash.Success"] = $"Card {card.CardUid} issued to {card.UserId}.";
        return RedirectToAction(nameof(Detail), new { id = card.CardId });
    }

    // ─── Lifecycle transitions ────────────────────────────────────────────

    [HttpPost("{id:int}/block")]       [ValidateAntiForgeryToken]
    public Task<IActionResult> Block(int id, string? reason, CancellationToken ct)
        => Bounce(id, _cards.BlockAsync(id, reason, User.Identity?.Name, ct));

    [HttpPost("{id:int}/unblock")]     [ValidateAntiForgeryToken]
    public Task<IActionResult> Unblock(int id, string? reason, CancellationToken ct)
        => Bounce(id, _cards.UnblockAsync(id, reason, User.Identity?.Name, ct));

    [HttpPost("{id:int}/report-lost")] [ValidateAntiForgeryToken]
    public Task<IActionResult> ReportLost(int id, string? reason, CancellationToken ct)
        => Bounce(id, _cards.ReportLostAsync(id, reason, User.Identity?.Name, ct));

    [HttpPost("{id:int}/activate")]    [ValidateAntiForgeryToken]
    public Task<IActionResult> Activate(int id, CancellationToken ct)
        => Bounce(id, _cards.ActivateAsync(id, User.Identity?.Name, ct));

    [HttpPost("{id:int}/retire")]      [ValidateAntiForgeryToken]
    public Task<IActionResult> Retire(int id, string? reason, CancellationToken ct)
        => Bounce(id, _cards.RetireAsync(id, reason, User.Identity?.Name, ct));

    public sealed class ReassignForm
    {
        public int CardId { get; set; }
        public string NewCardUid { get; set; } = string.Empty;
        public string? Reason { get; set; }
    }

    [HttpPost("{id:int}/reassign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reassign(int id, ReassignForm form, CancellationToken ct)
    {
        var result = await _cards.ReassignAsync(id, new CardReassignInput(form.NewCardUid, form.Reason),
            performedBy: User.Identity?.Name, ct);
        if (!result.IsSuccess)
        {
            TempData["Flash.Error"] = result.Error.Message ?? "Could not reassign card.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        var card = result.Value!;
        TempData["Flash.Success"] = $"Card re-issued to UID {card.CardUid}.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    private async Task<IActionResult> Bounce(int id, Task<Platform.Application.Results.Result<NfcCard>> work)
    {
        var result = await work;
        if (!result.IsSuccess)
        {
            TempData["Flash.Error"] = result.Error.Message ?? "Transition failed.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        TempData["Flash.Success"] = $"Card {result.Value!.CardUid} updated.";
        return RedirectToAction(nameof(Detail), new { id });
    }
}
