// =============================================================================
// CacheService  (Infrastructure)
// -----------------------------------------------------------------------------
// Concrete two-tier cache implementing ICacheService.
//
// TIER LAYOUT
//   L1 = IMemoryCache (process-local, ~microsecond)
//   L2 = IDistributedCache (Redis if configured, else in-memory mirror)
//
// TAG INDEX
//   We maintain a tiny Redis SET per tag containing the set of physical keys
//   that belong to it. Invalidate-by-tag wipes every member, then deletes the
//   set. This is the same approach used by NestJS cache-manager and
//   Microsoft's HybridCache (preview).
//
// PERF NOTES
//   - Serialisation is System.Text.Json with WhenWritingNull for compactness.
//   - The L1 entry is kept for the same TTL as L2 to keep them coherent.
//   - GetOrSet uses a *per-key* SemaphoreSlim to prevent thundering herd when
//     the cache is cold and several requests race for the same factory.
// =============================================================================

using System.Collections.Concurrent;
using System.Text.Json;
using Platform.Application.Abstractions.Caching;
using Platform.Application.Caching;
using Platform.Domain.Tenancy;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Platform.Infrastructure.Caching;

public sealed class CacheService : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    private readonly IMemoryCache _l1;
    private readonly IDistributedCache _l2;
    private readonly ITenantContext _tenant;
    private readonly ILogger<CacheService> _logger;

    public CacheService(IMemoryCache l1, IDistributedCache l2, ITenantContext tenant, ILogger<CacheService> logger)
    {
        _l1 = l1; _l2 = l2; _tenant = tenant; _logger = logger;
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? ttl = null, string[]? tags = null, CancellationToken cancellationToken = default) where T : class
    {
        var physicalKey = TenantKey(key);

        if (_l1.TryGetValue<T>(physicalKey, out var l1Hit) && l1Hit is not null) return l1Hit;

        var slim = _locks.GetOrAdd(physicalKey, _ => new SemaphoreSlim(1, 1));
        await slim.WaitAsync(cancellationToken);
        try
        {
            if (_l1.TryGetValue<T>(physicalKey, out l1Hit) && l1Hit is not null) return l1Hit;

            var raw = await _l2.GetStringAsync(physicalKey, cancellationToken);
            if (raw is not null)
            {
                var deserialized = JsonSerializer.Deserialize<T>(raw, JsonOptions);
                if (deserialized is not null)
                {
                    _l1.Set(physicalKey, deserialized, ttl ?? CacheTtl.Short);
                    return deserialized;
                }
            }

            var value = await factory(cancellationToken);
            await StoreAsync(physicalKey, value, ttl, tags, cancellationToken);
            return value;
        }
        finally
        {
            slim.Release();
        }
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        var physicalKey = TenantKey(key);
        if (_l1.TryGetValue<T>(physicalKey, out var l1Hit)) return l1Hit;

        var raw = await _l2.GetStringAsync(physicalKey, cancellationToken);
        if (raw is null) return null;

        var value = JsonSerializer.Deserialize<T>(raw, JsonOptions);
        if (value is not null) _l1.Set(physicalKey, value, CacheTtl.Short);
        return value;
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, string[]? tags = null, CancellationToken cancellationToken = default) where T : class
        => StoreAsync(TenantKey(key), value, ttl, tags, cancellationToken);

    public async Task InvalidateAsync(string key, CancellationToken cancellationToken = default)
    {
        var physicalKey = TenantKey(key);
        _l1.Remove(physicalKey);
        await _l2.RemoveAsync(physicalKey, cancellationToken);
    }

    public async Task InvalidateTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        var tagKey = TagIndexKey(tag);
        var raw = await _l2.GetStringAsync(tagKey, cancellationToken);
        if (raw is null) return;

        var members = JsonSerializer.Deserialize<HashSet<string>>(raw, JsonOptions) ?? new();
        foreach (var member in members)
        {
            _l1.Remove(member);
            await _l2.RemoveAsync(member, cancellationToken);
        }
        await _l2.RemoveAsync(tagKey, cancellationToken);
        _logger.LogInformation("Invalidated {Count} cache entries under tag {Tag}", members.Count, tag);
    }

    // -------------------- internals ------------------------------------------
    private async Task StoreAsync<T>(string physicalKey, T value, TimeSpan? ttl, string[]? tags, CancellationToken cancellationToken) where T : class
    {
        var effectiveTtl = ttl ?? CacheTtl.Short;
        var payload = JsonSerializer.Serialize(value, JsonOptions);

        await _l2.SetStringAsync(physicalKey, payload, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = effectiveTtl
        }, cancellationToken);

        _l1.Set(physicalKey, value, effectiveTtl);

        if (tags is { Length: > 0 })
        {
            foreach (var tag in tags) await AddToTagIndexAsync(tag, physicalKey, cancellationToken);
        }
    }

    private async Task AddToTagIndexAsync(string tag, string physicalKey, CancellationToken cancellationToken)
    {
        var tagKey = TagIndexKey(tag);
        var raw = await _l2.GetStringAsync(tagKey, cancellationToken);
        var members = raw is null ? new HashSet<string>() : (JsonSerializer.Deserialize<HashSet<string>>(raw, JsonOptions) ?? new());
        members.Add(physicalKey);
        await _l2.SetStringAsync(tagKey, JsonSerializer.Serialize(members, JsonOptions), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheTtl.Day
        }, cancellationToken);
    }

    private string TenantKey(string logicalKey)
    {
        var tenant = string.IsNullOrEmpty(_tenant.ClientCode) ? "default" : _tenant.ClientCode;
        return $"tenant:{tenant}:{logicalKey}";
    }

    private string TagIndexKey(string tag) => $"{TenantKey("tag")}:{tag}";
}
