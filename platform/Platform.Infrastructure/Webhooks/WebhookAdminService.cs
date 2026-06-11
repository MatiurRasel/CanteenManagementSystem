// =============================================================================
// WebhookAdminService  (Platform.Infrastructure.Webhooks)
// -----------------------------------------------------------------------------
// Default IWebhookAdminService impl. ADR 0004 — IUnitOfWork only.
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions.Webhooks;
using Platform.Application.Persistence;
using Platform.Application.Results;
using Platform.Domain.Webhooks;

namespace Platform.Infrastructure.Webhooks;

public sealed class WebhookAdminService : IWebhookAdminService
{
    private readonly IUnitOfWork _uow;
    public WebhookAdminService(IUnitOfWork uow) => _uow = uow;

    private IRepository<WebhookSubscription> Subs       => _uow.Repository<WebhookSubscription>();
    private IRepository<WebhookDelivery>     Deliveries => _uow.Repository<WebhookDelivery>();

    public async Task<IReadOnlyList<WebhookSubscription>> ListSubscriptionsAsync(CancellationToken ct = default)
    {
        var rows = await Subs.ListAsync(ct);
        return rows.OrderBy(s => s.DisplayName).ToList();
    }

    public Task<WebhookSubscription?> GetByIdAsync(int subscriptionId, CancellationToken ct = default)
        => Subs.FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId, ct);

    public async Task<Result<WebhookSubscription>> SaveAsync(WebhookSubscriptionInput input, CancellationToken ct = default)
    {
        if (input.SubscriptionId == 0)
        {
            var row = new WebhookSubscription
            {
                DisplayName  = input.DisplayName.Trim(),
                Url          = input.Url.Trim(),
                Secret       = input.Secret.Trim(),
                EventsCsv    = NormaliseEvents(input.EventsCsv),
                IsActive     = input.IsActive,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy    = input.PerformedBy
            };
            await Subs.AddAsync(row, ct);
            await _uow.SaveChangesAsync(ct);
            return Result.Success(row);
        }

        var existing = await Subs.FirstOrDefaultAsync(s => s.SubscriptionId == input.SubscriptionId, ct);
        if (existing is null) return Result.Failure<WebhookSubscription>(Error.NotFound("Subscription not found."));

        existing.DisplayName = input.DisplayName.Trim();
        existing.Url         = input.Url.Trim();
        existing.Secret      = input.Secret.Trim();
        existing.EventsCsv   = NormaliseEvents(input.EventsCsv);
        existing.IsActive    = input.IsActive;
        Subs.Update(existing);
        await _uow.SaveChangesAsync(ct);
        return Result.Success(existing);
    }

    public async Task<Result> ToggleAsync(int subscriptionId, CancellationToken ct = default)
    {
        var row = await Subs.FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId, ct);
        if (row is null) return Result.Failure(Error.NotFound("Subscription not found."));
        row.IsActive = !row.IsActive;
        if (row.IsActive) row.FailureCount = 0;   // resuming clears the circuit-breaker counter
        Subs.Update(row);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(int subscriptionId, CancellationToken ct = default)
    {
        var row = await Subs.FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId, ct);
        if (row is null) return Result.Failure(Error.NotFound("Subscription not found."));
        Subs.Remove(row);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<IReadOnlyList<WebhookDelivery>> ListDeliveriesAsync(int subscriptionId, int take = 100, CancellationToken ct = default)
    {
        var list = await Deliveries.NoTrackingQuery()
            .Where(d => d.SubscriptionId == subscriptionId)
            .OrderByDescending(d => d.LastAttemptAtUtc ?? d.FirstQueuedAtUtc)
            .Take(take)
            .ToListAsync(ct);
        return list;
    }

    public async Task<Result> ReplayDeliveryAsync(int subscriptionId, long deliveryId, CancellationToken ct = default)
    {
        var d = await Deliveries.FirstOrDefaultAsync(x => x.DeliveryId == deliveryId && x.SubscriptionId == subscriptionId, ct);
        if (d is null) return Result.Failure(Error.NotFound("Delivery not found."));
        d.Status = "Queued";
        d.NextRetryAtUtc = DateTime.UtcNow;
        d.ResponseSnippet = null;
        d.ResponseCode = 0;
        Deliveries.Update(d);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static string NormaliseEvents(string csv)
        => string.Join(',', csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                               .Select(s => s.ToLowerInvariant()).Distinct());
}
