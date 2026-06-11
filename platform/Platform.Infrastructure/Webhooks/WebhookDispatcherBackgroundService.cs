// =============================================================================
// WebhookDispatcherBackgroundService  (Platform.Infrastructure.Webhooks)
// -----------------------------------------------------------------------------
// Drains queued WebhookDelivery rows by POSTing to each subscription's URL
// with HMAC headers (X-App-Key + X-App-Timestamp + X-App-Signature — same
// scheme as the inbound auth so partners write ONE verifier for both flows).
//
//   * Polls every 5 s for due deliveries (NextRetryAtUtc <= now, Status in Queued/Failed).
//   * Marks Delivering → Delivered (HTTP 2xx) / Failed (else).
//   * Backoff: 30s, 2m, 10m, 30m, 2h, 6h, 24h, then 24h cap until manually replayed.
//   * Per-subscription circuit breaker: 8 consecutive failures → IsActive=false
//     (admin replays from UI; the FailureCount drives the dashboard badge).
//
// SCOPE — runs across tenants with the EF query filter OFF, since deliveries
// already carry their (tenant-scoped) SubscriptionId.
// =============================================================================

using System.Diagnostics.Metrics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.Application.Persistence;
using Platform.Domain.Webhooks;

namespace Platform.Infrastructure.Webhooks;

public sealed class WebhookDispatcherBackgroundService : BackgroundService
{
    public static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan[] Backoffs   =
    {
        TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2),  TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(30), TimeSpan.FromHours(2),    TimeSpan.FromHours(6),
        TimeSpan.FromHours(24)
    };
    private const int CircuitBreakerFailures = 8;

    // ─── Prometheus counters ────────────────────────────────────────────
    // Exposed via the `Platform.Webhooks` meter — picked up by the
    // OpenTelemetry Prometheus exporter on /metrics.
    private static readonly Meter Meter = new("Platform.Webhooks");
    private static readonly Counter<long> Attempts =
        Meter.CreateCounter<long>("webhook.delivery.attempts", "deliveries",
            "Total webhook delivery attempts (delivered + failed).");
    private static readonly Counter<long> Delivered =
        Meter.CreateCounter<long>("webhook.delivery.delivered", "deliveries",
            "Webhook deliveries that returned 2xx.");
    private static readonly Counter<long> Failed =
        Meter.CreateCounter<long>("webhook.delivery.failed", "deliveries",
            "Webhook deliveries that returned non-2xx or threw an exception.");
    private static readonly Counter<long> CircuitOpened =
        Meter.CreateCounter<long>("webhook.circuit.opened", "subscriptions",
            "Subscriptions parked because the failure threshold was crossed.");
    private static readonly Histogram<double> DurationMs =
        Meter.CreateHistogram<double>("webhook.delivery.duration", "ms",
            "Wall-clock duration of each delivery attempt.");

    private readonly IServiceProvider _sp;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<WebhookDispatcherBackgroundService> _logger;

    public WebhookDispatcherBackgroundService(IServiceProvider sp, IHttpClientFactory http,
        ILogger<WebhookDispatcherBackgroundService> logger)
    {
        _sp = sp; _http = http; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Webhook dispatcher started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "Webhook dispatcher tick failed."); }
            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        await using var scope = _sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var now = DateTime.UtcNow;
        var due = await db.Set<WebhookDelivery>().IgnoreQueryFilters()
            .Where(d => (d.Status == "Queued" || d.Status == "Failed")
                     && (d.NextRetryAtUtc == null || d.NextRetryAtUtc <= now))
            .OrderBy(d => d.FirstQueuedAtUtc)
            .Take(50)
            .ToListAsync(ct);
        if (due.Count == 0) return;

        var subscriptions = await db.Set<WebhookSubscription>().IgnoreQueryFilters()
            .Where(s => due.Select(d => d.SubscriptionId).Contains(s.SubscriptionId))
            .ToListAsync(ct);
        var byId = subscriptions.ToDictionary(s => s.SubscriptionId);
        var http = _http.CreateClient("webhook.dispatcher");

        foreach (var d in due)
        {
            if (!byId.TryGetValue(d.SubscriptionId, out var sub) || !sub.IsActive)
            {
                d.Status = "Failed";
                d.ResponseSnippet = "Subscription inactive or deleted.";
                d.CompletedAtUtc = now;
                continue;
            }
            await DispatchAsync(http, d, sub, ct);
        }

        await db.SaveChangesAsync(CancellationToken.None);
    }

    private async Task DispatchAsync(HttpClient http, WebhookDelivery d, WebhookSubscription sub, CancellationToken ct)
    {
        d.Status = "Delivering";
        d.Attempt++;
        d.LastAttemptAtUtc = DateTime.UtcNow;
        var eventTag = new KeyValuePair<string, object?>("event", d.EventKey);
        Attempts.Add(1, eventTag);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, sub.Url);
            var payloadBytes = Encoding.UTF8.GetBytes(d.PayloadJson);
            req.Content = new ByteArrayContent(payloadBytes);
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            Sign(req, sub, payloadBytes);
            req.Headers.TryAddWithoutValidation("X-Event-Key", d.EventKey);

            using var res = await http.SendAsync(req, ct);
            d.ResponseCode = (int)res.StatusCode;
            var body = await SafelyReadAsync(res, ct);
            d.ResponseSnippet = body.Length <= 2000 ? body : body[..2000];

            if (res.IsSuccessStatusCode)
            {
                d.Status = "Delivered";
                d.CompletedAtUtc = DateTime.UtcNow;
                d.NextRetryAtUtc = null;
                sub.LastSuccessAtUtc = d.CompletedAtUtc;
                sub.FailureCount = 0;
                Delivered.Add(1, eventTag);
                return;
            }

            // Non-2xx → retry with backoff.
            Failed.Add(1, eventTag, new KeyValuePair<string, object?>("status", (int)res.StatusCode));
            MarkRetry(d, sub);
        }
        catch (Exception ex)
        {
            d.ResponseCode = 0;
            d.ResponseSnippet = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            Failed.Add(1, eventTag, new KeyValuePair<string, object?>("status", "exception"));
            MarkRetry(d, sub);
        }
        finally
        {
            sw.Stop();
            DurationMs.Record(sw.Elapsed.TotalMilliseconds, eventTag);
        }
    }

    private static void MarkRetry(WebhookDelivery d, WebhookSubscription sub)
    {
        d.Status = "Failed";
        sub.LastFailureAtUtc = DateTime.UtcNow;
        sub.FailureCount++;
        var step = Math.Min(d.Attempt - 1, Backoffs.Length - 1);
        d.NextRetryAtUtc = DateTime.UtcNow + Backoffs[Math.Max(0, step)];
        if (sub.FailureCount >= CircuitBreakerFailures)
        {
            sub.IsActive = false;          // park the subscription; admin re-enables after fixing the endpoint
            CircuitOpened.Add(1);
        }
    }

    private static void Sign(HttpRequestMessage req, WebhookSubscription sub, byte[] body)
    {
        var ts        = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var bodyHash  = Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant();
        var path      = req.RequestUri?.AbsolutePath ?? "/";
        var canonical = $"webhook:{sub.SubscriptionId}\n{ts}\nPOST\n{path}\n{bodyHash}";
        var sig       = Convert.ToBase64String(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(sub.Secret), Encoding.UTF8.GetBytes(canonical)));

        req.Headers.TryAddWithoutValidation("X-App-Key",       $"webhook:{sub.SubscriptionId}");
        req.Headers.TryAddWithoutValidation("X-App-Timestamp", ts);
        req.Headers.TryAddWithoutValidation("X-App-Signature", sig);
    }

    private static async Task<string> SafelyReadAsync(HttpResponseMessage res, CancellationToken ct)
    {
        try { return await res.Content.ReadAsStringAsync(ct); }
        catch { return string.Empty; }
    }
}
