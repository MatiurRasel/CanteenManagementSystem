using System.Text.Json;
using Platform.Application.Abstractions.Caching;
using Platform.Domain.Tenancy;
using Microsoft.Extensions.Caching.Distributed;

namespace Platform.Infrastructure.Caching;

/// IDistributedCache wrapper that scopes every key by tenant so two tenants
/// sharing the same Redis instance cannot collide.
public sealed class TenantAwareDistributedCacheService : IDistributedCacheService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IDistributedCache _cache;
    private readonly ITenantContext _tenant;

    public TenantAwareDistributedCacheService(IDistributedCache cache, ITenantContext tenant)
    {
        _cache = cache;
        _tenant = tenant;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        var raw = await _cache.GetStringAsync(TenantKey(key), cancellationToken);
        return raw is null ? null : JsonSerializer.Deserialize<T>(raw, _jsonOptions);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default) where T : class
    {
        var options = new DistributedCacheEntryOptions();
        if (ttl.HasValue) options.AbsoluteExpirationRelativeToNow = ttl;
        await _cache.SetStringAsync(TenantKey(key), JsonSerializer.Serialize(value, _jsonOptions), options, cancellationToken);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        => _cache.RemoveAsync(TenantKey(key), cancellationToken);

    public async Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? ttl = null, CancellationToken cancellationToken = default) where T : class
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached is not null) return cached;

        var value = await factory(cancellationToken);
        if (value is not null)
        {
            await SetAsync(key, value, ttl, cancellationToken);
        }
        return value!;
    }

    private string TenantKey(string key)
    {
        var tenantSegment = string.IsNullOrEmpty(_tenant.ClientCode) ? "default" : _tenant.ClientCode;
        return $"tenant:{tenantSegment}:{key}";
    }
}
