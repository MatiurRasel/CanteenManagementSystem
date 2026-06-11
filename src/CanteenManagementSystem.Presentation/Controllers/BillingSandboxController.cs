// =============================================================================
// BillingSandboxController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Test hooks for the SandboxBillingService — let an admin drive the
// subscription state machine for demos without paying anything.
//
//   POST /admin/billing/sandbox/simulate/{state}   — flip to PastDue / Active / Cancelled / Trialing
//   GET  /admin/billing/sandbox/invoice/{id}       — synthetic invoice HTML
// =============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Billing;
using Platform.Infrastructure.Billing;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "TenantAdmin")]
[Route("admin/billing/sandbox")]
public sealed class BillingSandboxController : Controller
{
    private readonly IBillingService _billing;
    public BillingSandboxController(IBillingService billing) => _billing = billing;

    [HttpPost("simulate/{state}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Simulate(string state, CancellationToken ct)
    {
        if (_billing is SandboxBillingService sbx
            && Enum.TryParse<SubscriptionState>(state, true, out var s))
        {
            await sbx.SimulateAsync(s, ct);
            TempData["Flash.Success"] = $"Sandbox billing state → {s}";
        }
        else
        {
            TempData["Flash.Error"] = "Sandbox state simulator only works when IBillingService is SandboxBillingService.";
        }
        return Redirect("/admin/billing");
    }

    [HttpGet("invoice/{id}")]
    public IActionResult Invoice(string id)
    {
        // CSS braces collide with C# interpolation in raw strings, so we
        // stitch the dynamic bits (id + today's date) with String.Concat.
        var safeId = System.Net.WebUtility.HtmlEncode(id);
        var today  = DateTime.UtcNow.ToString("yyyy-MM-dd");
        const string head = @"<!doctype html><html><head><meta charset='utf-8'><title>Sandbox invoice</title>
<style>
  body{font-family:Inter,system-ui,sans-serif;color:#27272a;background:#fafafa;padding:32px;max-width:640px;margin:0 auto}
  h1{margin:0 0 4px} table{width:100%;border-collapse:collapse;margin-top:16px}
  th,td{text-align:left;padding:8px 0;border-bottom:1px solid #e4e4e7;font-size:14px}
  .total{font-weight:600;font-size:18px} .muted{color:#71717a;font-size:12px}
</style></head><body>
  <h1>Sandbox invoice</h1>
  <p class='muted'>This is a synthetic invoice from the SandboxBillingService — for demo only.</p>
  <table>";
        const string foot = @"
    <tr><th>Status</th><td>Paid (simulated)</td></tr>
    <tr><th>Line</th><td>Canteen SaaS subscription · 30 days</td></tr>
    <tr><td class='total'>Total</td><td class='total'>USD 99.00</td></tr>
  </table>
  <p class='muted' style='margin-top:32px'>Switch to live billing by setting <code>Billing.Provider=Stripe</code> + pasting <code>Billing.Stripe.ApiKey</code>.</p>
</body></html>";
        var middle = "<tr><th>Invoice id</th><td>" + safeId + "</td></tr>"
                   + "<tr><th>Issued</th><td>" + today + "</td></tr>";
        return Content(head + middle + foot, "text/html");
    }
}
