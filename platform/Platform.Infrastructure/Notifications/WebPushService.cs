// =============================================================================
// WebPushService  (Platform.Infrastructure.Notifications)
// -----------------------------------------------------------------------------
// Stores PushSubscription rows + sends VAPID-signed Web Push notifications.
//
// SEND FLOW
//   The minimal RFC 8291 payload is intentionally NOT encrypted server-side
//   in this implementation — that requires ECDH agreement + AES-128-GCM and
//   would lock us into either WebPushVapid 4.x (Apache, fine) or a hand-roll.
//   Instead we POST the canonical JSON to the endpoint with VAPID Auth +
//   urgency headers; the browser's service worker resolves the JSON payload
//   from a small fetch by tag once it wakes — see /js/sw.js push event.
//
// VAPID SIGNATURE
//   JWT signed with ES256 over the endpoint origin + expiry + subject claim.
//   The signed Authorization header lets push services (Mozilla autopush,
//   FCM, Apple) verify the sender without a per-sender contract.
// =============================================================================

using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Abstractions.Notifications;
using Platform.Application.Persistence;
using Platform.Domain.Notifications;

namespace Platform.Infrastructure.Notifications;

public sealed class WebPushService : IWebPushService
{
    private readonly IUnitOfWork _uow;
    private readonly IReadOnlyRepository<PushSubscription> _subs;
    private readonly ITenantSettings _settings;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<WebPushService> _logger;

    public WebPushService(
        IUnitOfWork uow,
        IReadOnlyRepository<PushSubscription> subs,
        ITenantSettings settings,
        IHttpClientFactory http,
        ILogger<WebPushService> logger)
    {
        _uow = uow; _subs = subs; _settings = settings; _http = http; _logger = logger;
    }

    private IRepository<PushSubscription> WriteSubs => _uow.Repository<PushSubscription>();

    public Task<string?> GetPublicKeyAsync(CancellationToken ct = default)
        => _settings.GetAsync("WebPush.VapidPublicKey", defaultValue: null, ct);

    public async Task<PushSubscription> RegisterAsync(string userId, string endpoint, string p256dh, string auth, string? userAgent, CancellationToken ct = default)
    {
        var existing = await WriteSubs.FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == endpoint, ct);
        if (existing is not null)
        {
            existing.P256dh = p256dh; existing.Auth = auth;
            existing.UserAgent = userAgent;
            existing.IsActive = true;
            existing.LastSeenAtUtc = DateTime.UtcNow;
            await _uow.SaveChangesAsync(ct);
            return existing;
        }
        var row = new PushSubscription
        {
            UserId = userId, Endpoint = endpoint, P256dh = p256dh, Auth = auth,
            UserAgent = userAgent, IsActive = true,
            CreatedAtUtc = DateTime.UtcNow, LastSeenAtUtc = DateTime.UtcNow
        };
        await WriteSubs.AddAsync(row, ct);
        await _uow.SaveChangesAsync(ct);
        return row;
    }

    public async Task<bool> RemoveAsync(string userId, string endpoint, CancellationToken ct = default)
    {
        var row = await WriteSubs.FirstOrDefaultAsync(s => s.UserId == userId && s.Endpoint == endpoint, ct);
        if (row is null) return false;
        WriteSubs.Remove(row);
        await _uow.SaveChangesAsync(ct);
        return true;
    }

    public async Task<WebPushSendResult> SendToUserAsync(string userId, WebPushPayload payload, CancellationToken ct = default)
    {
        var subject  = await _settings.GetAsync("WebPush.SubjectMailto", "mailto:ops@example.com", ct);
        var pubKey   = await _settings.GetAsync("WebPush.VapidPublicKey",  defaultValue: null, ct);
        var privKey  = await _settings.GetAsync("WebPush.VapidPrivateKey", defaultValue: null, ct);

        if (string.IsNullOrWhiteSpace(pubKey) || string.IsNullOrWhiteSpace(privKey))
        {
            _logger.LogWarning("Web push VAPID keys missing — skipping send for {User}", userId);
            return new WebPushSendResult(0, 0, 0);
        }

        var subs = await _subs.NoTrackingQuery()
            .Where(s => s.UserId == userId && s.IsActive)
            .ToListAsync(ct);
        if (subs.Count == 0) return new WebPushSendResult(0, 0, 0);

        var http = _http.CreateClient("webpush.dispatcher");
        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

        int delivered = 0, removed = 0;
        foreach (var s in subs)
        {
            var uri = new Uri(s.Endpoint);
            var origin = $"{uri.Scheme}://{uri.Host}";
            var jwt = BuildVapidJwt(origin, subject ?? "mailto:ops@example.com", privKey!);

            using var req = new HttpRequestMessage(HttpMethod.Post, s.Endpoint);
            req.Headers.TryAddWithoutValidation("TTL", "60");
            req.Headers.TryAddWithoutValidation("Urgency", "normal");
            req.Headers.Authorization = new AuthenticationHeaderValue("vapid", $"t={jwt}, k={pubKey}");
            req.Content = new ByteArrayContent(payloadBytes);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            try
            {
                using var res = await http.SendAsync(req, ct);
                if ((int)res.StatusCode is >= 200 and < 300) { delivered++; continue; }

                if (res.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
                {
                    // Subscription dead — drop it.
                    var dead = await WriteSubs.FirstOrDefaultAsync(x => x.PushSubscriptionId == s.PushSubscriptionId, ct);
                    if (dead is not null) { dead.IsActive = false; removed++; }
                }
                else
                {
                    _logger.LogWarning("Web push POST returned {Status} for {Endpoint}", (int)res.StatusCode, s.Endpoint);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Web push send threw for {Endpoint}", s.Endpoint);
            }
        }
        if (removed > 0) await _uow.SaveChangesAsync(ct);
        return new WebPushSendResult(subs.Count, delivered, removed);
    }

    /// <summary>VAPID JWT, ES256 over a minimal header.body.signature triple.</summary>
    private static string BuildVapidJwt(string aud, string sub, string base64UrlPrivateKey)
    {
        var header  = JsonSerializer.SerializeToUtf8Bytes(new { typ = "JWT", alg = "ES256" });
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            aud,
            exp = DateTimeOffset.UtcNow.AddHours(6).ToUnixTimeSeconds(),
            sub
        });
        var signingInput = $"{Base64Url(header)}.{Base64Url(payload)}";
        var pk = Base64UrlDecode(base64UrlPrivateKey);

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(pk, out _);
        var sig = ecdsa.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{signingInput}.{Base64Url(sig)}";
    }

    private static string Base64Url(byte[] data)
        => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
        return Convert.FromBase64String(s);
    }
}
