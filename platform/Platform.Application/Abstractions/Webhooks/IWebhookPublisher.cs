// =============================================================================
// IWebhookPublisher  (Platform.Application.Abstractions.Webhooks)
// -----------------------------------------------------------------------------
// What the rest of the codebase calls when something interesting happens. Fire
// once with the event key + payload object; the impl resolves all matching
// subscriptions for the current tenant and queues one WebhookDelivery row per
// subscriber.
//
// FIRE-AND-FORGET — never throws. Failures are recorded on the WebhookDelivery
// row + retried by the background worker; they never bubble to the caller.
// =============================================================================

namespace Platform.Application.Abstractions.Webhooks;

public interface IWebhookPublisher
{
    Task PublishAsync(string eventKey, object payload, CancellationToken cancellationToken = default);
}

/// <summary>Canonical event keys the platform fires. Partners filter by these.</summary>
public static class WebhookEvents
{
    public const string OrderPlaced     = "order.placed";
    public const string OrderDelivered  = "order.delivered";
    public const string OrderVoided     = "order.voided";
    public const string OrderRefunded   = "order.refunded";
    public const string WalletRecharged = "wallet.recharged";
    public const string CardLost        = "card.lost";
    public const string DirectorySynced = "directory.synced";
}
