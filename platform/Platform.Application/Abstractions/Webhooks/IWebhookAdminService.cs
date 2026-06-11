// =============================================================================
// IWebhookAdminService  (Platform.Application.Abstractions.Webhooks)
// -----------------------------------------------------------------------------
// Admin CRUD over WebhookSubscription + delivery history + replay. Used by
// /admin/webhooks (ADR 0004).
// =============================================================================

using Platform.Application.Results;
using Platform.Domain.Webhooks;

namespace Platform.Application.Abstractions.Webhooks;

public sealed record WebhookSubscriptionInput(
    int SubscriptionId,
    string DisplayName, string Url, string Secret, string EventsCsv, bool IsActive,
    string? PerformedBy);

public interface IWebhookAdminService
{
    Task<IReadOnlyList<WebhookSubscription>> ListSubscriptionsAsync(CancellationToken ct = default);
    Task<WebhookSubscription?> GetByIdAsync(int subscriptionId, CancellationToken ct = default);

    Task<Result<WebhookSubscription>> SaveAsync(WebhookSubscriptionInput input, CancellationToken ct = default);
    Task<Result> ToggleAsync(int subscriptionId, CancellationToken ct = default);
    Task<Result> DeleteAsync(int subscriptionId, CancellationToken ct = default);

    Task<IReadOnlyList<WebhookDelivery>> ListDeliveriesAsync(int subscriptionId, int take = 100, CancellationToken ct = default);
    Task<Result> ReplayDeliveryAsync(int subscriptionId, long deliveryId, CancellationToken ct = default);
}
