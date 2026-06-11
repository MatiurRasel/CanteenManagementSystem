// =============================================================================
// WebhooksAdminController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Per ADR 0004 the controller depends on IWebhookAdminService — no IAppDbContext.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Webhooks;
using Platform.Domain.Webhooks;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "TenantAdmin")]
[Route("admin/webhooks")]
public sealed class WebhooksAdminController : Controller
{
    private readonly IWebhookAdminService _webhooks;
    public WebhooksAdminController(IWebhookAdminService webhooks) => _webhooks = webhooks;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _webhooks.ListSubscriptionsAsync(ct));

    public sealed class SubForm
    {
        public int SubscriptionId { get; set; }
        [Required, StringLength(128)] public string DisplayName { get; set; } = string.Empty;
        [Required, StringLength(512), Url] public string Url { get; set; } = string.Empty;
        [Required, StringLength(256)] public string Secret { get; set; } = string.Empty;
        [Required] public string EventsCsv { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    [HttpGet("new")]
    public IActionResult New() => View("Edit", new SubForm
    {
        Secret    = GenerateSecret(),
        EventsCsv = "order.placed,order.delivered"
    });

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var row = await _webhooks.GetByIdAsync(id, ct);
        if (row is null) return NotFound();
        return View(new SubForm
        {
            SubscriptionId = row.SubscriptionId,
            DisplayName    = row.DisplayName,
            Url            = row.Url,
            Secret         = row.Secret,
            EventsCsv      = row.EventsCsv,
            IsActive       = row.IsActive
        });
    }

    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(SubForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Edit", form);
        var result = await _webhooks.SaveAsync(new WebhookSubscriptionInput(
            form.SubscriptionId, form.DisplayName, form.Url, form.Secret, form.EventsCsv, form.IsActive,
            PerformedBy: User.Identity?.Name), ct);
        if (!result.IsSuccess) return NotFound();
        TempData["Flash.Success"] = "Webhook subscription saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, CancellationToken ct)
    {
        var result = await _webhooks.ToggleAsync(id, ct);
        if (!result.IsSuccess) return NotFound();
        TempData["Flash.Success"] = "Subscription toggled.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _webhooks.DeleteAsync(id, ct);
        if (!result.IsSuccess) return NotFound();
        TempData["Flash.Warning"] = "Subscription removed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id:int}/deliveries")]
    public async Task<IActionResult> Deliveries(int id, CancellationToken ct)
    {
        var sub = await _webhooks.GetByIdAsync(id, ct);
        if (sub is null) return NotFound();
        var rows = await _webhooks.ListDeliveriesAsync(id, take: 100, ct);
        ViewBag.Subscription = sub;
        return View(rows);
    }

    [HttpPost("{id:int}/replay/{deliveryId:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Replay(int id, long deliveryId, CancellationToken ct)
    {
        var result = await _webhooks.ReplayDeliveryAsync(id, deliveryId, ct);
        if (!result.IsSuccess) return NotFound();
        TempData["Flash.Success"] = "Delivery re-queued for next dispatcher tick.";
        return RedirectToAction(nameof(Deliveries), new { id });
    }

    private static string GenerateSecret()
    {
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }
}
