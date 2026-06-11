// =============================================================================
// PushController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Browser-side Web Push subscription endpoints.
//
//   GET  /push/vapid          → VAPID public key (so the browser can subscribe)
//   POST /push/subscribe      → register the browser's PushSubscription JSON
//   POST /push/unsubscribe    → drop the registration
//
// The matching service worker lives at /js/sw.js — it's registered by
// /js/push-client.js when the user clicks the "enable notifications" button.
// =============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Notifications;
using System.Security.Claims;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize]
[Route("push")]
public sealed class PushController : Controller
{
    private readonly IWebPushService _push;
    public PushController(IWebPushService push) => _push = push;

    private string CurrentUserId() => User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";

    [HttpGet("vapid")]
    [AllowAnonymous]
    public async Task<IActionResult> Vapid(CancellationToken ct)
    {
        var key = await _push.GetPublicKeyAsync(ct);
        if (string.IsNullOrWhiteSpace(key))
        {
            return Json(new { configured = false, publicKey = (string?)null });
        }
        return Json(new { configured = true, publicKey = key });
    }

    public sealed class SubscribeRequest
    {
        public string Endpoint { get; set; } = string.Empty;
        public KeysObject Keys { get; set; } = new();
        public sealed class KeysObject
        {
            public string P256dh { get; set; } = string.Empty;
            public string Auth   { get; set; } = string.Empty;
        }
    }

    [HttpPost("subscribe")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint)) return BadRequest();
        var sub = await _push.RegisterAsync(CurrentUserId(), request.Endpoint,
            request.Keys.P256dh, request.Keys.Auth,
            Request.Headers.UserAgent.ToString(), ct);
        return Json(new { success = true, id = sub.PushSubscriptionId });
    }

    public sealed class UnsubscribeRequest { public string Endpoint { get; set; } = string.Empty; }

    [HttpPost("unsubscribe")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unsubscribe([FromBody] UnsubscribeRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Endpoint)) return BadRequest();
        var ok = await _push.RemoveAsync(CurrentUserId(), request.Endpoint, ct);
        return Json(new { success = ok });
    }
}
