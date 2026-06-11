// =============================================================================
// WebhookPublisher  (Platform.Infrastructure.Webhooks)
// -----------------------------------------------------------------------------
// Resolves the active WebhookSubscriptions for the current tenant whose
// EventsCsv contains the event key, then queues one WebhookDelivery row per
// match. The dispatcher background service picks them up.
//
// ADR 0004: IUnitOfWork + IReadOnlyRepository<T>.
// =============================================================================

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Application.Abstractions.Webhooks;
using Platform.Application.Persistence;
using Platform.Domain.Webhooks;

namespace Platform.Infrastructure.Webhooks;

public sealed class WebhookPublisher : IWebhookPublisher
{
    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IUnitOfWork _uow;
    private readonly IReadOnlyRepository<WebhookSubscription> _subs;
    private readonly ILogger<WebhookPublisher> _logger;

    public WebhookPublisher(IUnitOfWork uow, IReadOnlyRepository<WebhookSubscription> subs, ILogger<WebhookPublisher> logger)
    {
        _uow = uow; _subs = subs; _logger = logger;
    }

    public async Task PublishAsync(string eventKey, object payload, CancellationToken cancellationToken = default)
    {
        try
        {
            var subs = await _subs.NoTrackingQuery()
                .Where(s => s.IsActive && s.EventsCsv.Contains(eventKey))
                .ToListAsync(cancellationToken);
            if (subs.Count == 0) return;

            var body = JsonSerializer.Serialize(new
            {
                eventKey,
                publishedAtUtc = DateTime.UtcNow,
                data = payload
            }, Json);

            var now = DateTime.UtcNow;
            var deliveries = _uow.Repository<WebhookDelivery>();
            foreach (var s in subs)
            {
                await deliveries.AddAsync(new WebhookDelivery
                {
                    SubscriptionId   = s.SubscriptionId,
                    EventKey         = eventKey,
                    PayloadJson      = body,
                    Status           = "Queued",
                    Attempt          = 0,
                    FirstQueuedAtUtc = now,
                    NextRetryAtUtc   = now
                }, cancellationToken);
            }
            await _uow.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Webhook publish for {Event} failed at the queue step.", eventKey);
        }
    }
}
