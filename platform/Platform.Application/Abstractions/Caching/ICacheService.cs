// =============================================================================
// ICacheService  (Application abstraction)
// -----------------------------------------------------------------------------
// Two-tier cache:
//   L1  IMemoryCache (per-instance, microsecond reads)
//   L2  IDistributedCache (Redis or in-memory fallback, ms reads, cluster-safe)
//
// READ FLOW
//   1. L1 hit?  -> return immediately.
//   2. L2 hit?  -> populate L1 + return.
//   3. Factory  -> compute value, write L1 + L2, return.
//
// WRITE FLOW (cache invalidation)
//   - InvalidateAsync(key)    : evict from L1 + L2.
//   - InvalidateTagAsync(tag) : evict every key associated with the tag from
//     both tiers. Used when a domain event mutates a swath of data
//     (e.g. menu changed -> "menu" tag is wiped).
//
// TENANT SAFETY
//   Implementation prepends "tenant:{clientCode}:" so two tenants sharing the
//   same Redis instance can never collide. The caller passes the *logical*
//   key; the impl owns physical key construction.
// =============================================================================

namespace Platform.Application.Abstractions.Caching;

public interface ICacheService
{
    /// <summary>Get a value or compute+store it. Tenant-scoped automatically.</summary>
    Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? ttl = null,
        string[]? tags = null,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>Try-read without populating.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    /// <summary>Explicit set (e.g. after a write).</summary>
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, string[]? tags = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>Evict a single key from both tiers for the current tenant.</summary>
    Task InvalidateAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Evict every key associated with the tag from both tiers for the current tenant.</summary>
    Task InvalidateTagAsync(string tag, CancellationToken cancellationToken = default);
}
