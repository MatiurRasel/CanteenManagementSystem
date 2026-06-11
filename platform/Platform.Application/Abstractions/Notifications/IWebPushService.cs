// =============================================================================
// IWebPushService  (Platform.Application.Abstractions.Notifications)
// -----------------------------------------------------------------------------
// W3C Web Push (RFC 8030) — sends a payload to a browser even when the tab is
// closed. The browser surfaces a system notification via the registered
// service worker. Uses VAPID for sender identification — no Firebase needed.
//
// CONFIGURATION (tenant settings)
//   WebPush.VapidPublicKey   — base64url, served to clients via /push/vapid
//   WebPush.VapidPrivateKey  — base64url, secret
//   WebPush.SubjectMailto    — "mailto:ops@<your-org>" required by VAPID spec
// =============================================================================

using Platform.Domain.Notifications;

namespace Platform.Application.Abstractions.Notifications;

public sealed record WebPushPayload(string Title, string Body, string? Url, string? Tag, string? Icon);

public sealed record WebPushSendResult(int Attempted, int Delivered, int RemovedDead);

public interface IWebPushService
{
    /// <summary>Server-known public key handed to clients before they subscribe.</summary>
    Task<string?> GetPublicKeyAsync(CancellationToken ct = default);

    /// <summary>Persist a new subscription. Idempotent on (UserId, Endpoint).</summary>
    Task<PushSubscription> RegisterAsync(string userId, string endpoint, string p256dh, string auth, string? userAgent, CancellationToken ct = default);

    /// <summary>Drop a subscription (user clicked "unsubscribe" / browser revoked).</summary>
    Task<bool> RemoveAsync(string userId, string endpoint, CancellationToken ct = default);

    /// <summary>Fan out a payload to every active subscription of the user.</summary>
    Task<WebPushSendResult> SendToUserAsync(string userId, WebPushPayload payload, CancellationToken ct = default);
}
