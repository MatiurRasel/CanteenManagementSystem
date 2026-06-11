// =============================================================================
// BillingAdminController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Per-tenant billing dashboard. Reads through IBillingService so today's stub
// impl returns inert data and tomorrow's StripeBillingService talks to the
// real Stripe Billing API.
//
//   GET  /admin/billing                — current plan + period end + invoice link
//   POST /admin/billing/checkout/{plan}— start a hosted checkout
//   POST /admin/billing/cancel         — schedule cancellation at period end
// =============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Audit;
using Platform.Application.Abstractions.Billing;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "TenantAdmin")]
[Route("admin/billing")]
public sealed class BillingAdminController : Controller
{
    private readonly IBillingService _billing;
    private readonly IAuditTrail _audit;

    public BillingAdminController(IBillingService billing, IAuditTrail audit)
    {
        _billing = billing; _audit = audit;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var status = await _billing.GetStatusAsync(ct);
        return View(status);
    }

    [HttpPost("checkout/{plan}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(string plan, CancellationToken ct)
    {
        var success = $"{Request.Scheme}://{Request.Host}/admin/billing?checkout=success";
        var cancel  = $"{Request.Scheme}://{Request.Host}/admin/billing?checkout=cancelled";
        var result  = await _billing.StartCheckoutAsync(plan, success, cancel, ct);
        if (!result.Success || string.IsNullOrWhiteSpace(result.RedirectUrl))
        {
            TempData["Flash.Error"] = result.Message ?? "Checkout failed.";
            return RedirectToAction(nameof(Index));
        }
        await _audit.RecordAsync("Billing.CheckoutStarted", "Tenant", null, new { plan }, ct);
        return Redirect(result.RedirectUrl);
    }

    [HttpPost("cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(CancellationToken ct)
    {
        var ok = await _billing.CancelAsync(ct);
        await _audit.RecordAsync("Billing.CancellationRequested", "Tenant", null, new { ok }, ct);
        TempData[ok ? "Flash.Success" : "Flash.Error"] = ok
            ? "Subscription will cancel at period end."
            : "Cancellation failed — see logs.";
        return RedirectToAction(nameof(Index));
    }
}
