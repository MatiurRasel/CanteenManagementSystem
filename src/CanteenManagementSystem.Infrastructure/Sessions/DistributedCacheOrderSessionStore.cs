// =============================================================================
// DistributedCacheOrderSessionStore  (CanteenManagementSystem.Infrastructure.Sessions)
// -----------------------------------------------------------------------------
// Backs IOrderSessionStore with IDistributedCacheService, which falls back to
// an in-memory cache when Redis isn't configured AND prepends a tenant prefix
// automatically — so two tenants on the same server can't see each other's
// keypad sessions.
//
// Keys live under "kiosk:session:{id}".
// =============================================================================

using CanteenManagementSystem.Application.Sessions;
using CanteenManagementSystem.Domain.Sessions;
using Platform.Application.Abstractions.Caching;

namespace CanteenManagementSystem.Infrastructure.Sessions;

public sealed class DistributedCacheOrderSessionStore : IOrderSessionStore
{
    private const string KeyPrefix = "kiosk:session:";
    private readonly IDistributedCacheService _cache;

    public DistributedCacheOrderSessionStore(IDistributedCacheService cache) => _cache = cache;

    public Task<OrderSession?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
        => _cache.GetAsync<OrderSession>(KeyPrefix + sessionId, cancellationToken);

    public Task SetAsync(OrderSession session, TimeSpan ttl, CancellationToken cancellationToken = default)
        => _cache.SetAsync(KeyPrefix + session.SessionId, session, ttl, cancellationToken);

    public Task RemoveAsync(string sessionId, CancellationToken cancellationToken = default)
        => _cache.RemoveAsync(KeyPrefix + sessionId, cancellationToken);
}
