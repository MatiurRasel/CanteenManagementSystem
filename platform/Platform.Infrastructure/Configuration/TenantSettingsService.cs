// =============================================================================
// TenantSettingsService  (Infrastructure)
// -----------------------------------------------------------------------------
// Implementation of ITenantSettings. Reads from the database first, falling
// through appsettings.json, then a code-level default. Results are cached so
// the hot path is a dictionary hit, not a SQL round-trip.
//
// SETTING FLOW (read)
//   1. Resolve current tenant from ITenantContext.
//   2. Look up cached settings bag for that tenant.
//   3. On miss, SELECT * FROM CanteenTenantSettings WHERE ClientId = @t
//      and store in the cache for 1h tagged with the "tenant-settings" tag.
//   4. Try the requested key in the bag.
//   5. Fall back to IConfiguration[key with dots replaced by ":"].
//   6. Fall back to defaultValue.
//
// SETTING FLOW (write)
//   1. Upsert the row.
//   2. ICacheService.InvalidateTagAsync("tenant-settings") so the next reader
//      re-fetches the bag.
//   3. (Future) Publish a domain event so other instances clear their L1.
//
// TYPED ACCESSORS
//   GetIntAsync / GetDecimalAsync / GetBoolAsync do a Parse with InvariantCulture.
//   GetObjectAsync<T> JSON-deserialises the stored value — useful for complex
//   config blobs (e.g. "Payments.Bkash" -> a small JSON object).
// =============================================================================

using System.Globalization;
using System.Text.Json;
using Platform.Application.Persistence;
using Platform.Application.Abstractions.Caching;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Abstractions.Identity;
using Platform.Application.Abstractions.Time;
using Platform.Application.Caching;
using Platform.Domain.Tenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Platform.Infrastructure.Configuration;

public sealed class TenantSettingsService : ITenantSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    // Per ADR 0004 (Batch 2): no IAppDbContext. IUnitOfWork drives the
    // read+write+save flow; ICacheService keeps reads hot.
    private readonly IUnitOfWork _uow;
    private readonly ICacheService _cache;
    private readonly IConfiguration _configuration;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly ILogger<TenantSettingsService> _logger;

    public TenantSettingsService(
        IUnitOfWork uow,
        ICacheService cache,
        IConfiguration configuration,
        ICurrentUser currentUser,
        IClock clock,
        ILogger<TenantSettingsService> logger)
    {
        _uow = uow;
        _cache = cache;
        _configuration = configuration;
        _currentUser = currentUser;
        _clock = clock;
        _logger = logger;
    }

    public async Task<string?> GetAsync(string key, string? defaultValue = null, CancellationToken cancellationToken = default)
    {
        var bag = await GetBagAsync(cancellationToken);

        if (bag.TryGetValue(key, out var dbValue) && !string.IsNullOrEmpty(dbValue))
        {
            return dbValue;
        }

        // appsettings.json keys use ":" — translate "Payments.Bkash.AppKey" -> "Payments:Bkash:AppKey".
        var configKey = key.Replace('.', ':');
        var configValue = _configuration[configKey];
        if (!string.IsNullOrEmpty(configValue)) return configValue;

        return defaultValue;
    }

    public async Task<int> GetIntAsync(string key, int defaultValue = 0, CancellationToken cancellationToken = default)
    {
        var raw = await GetAsync(key, null, cancellationToken);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : defaultValue;
    }

    public async Task<decimal> GetDecimalAsync(string key, decimal defaultValue = 0m, CancellationToken cancellationToken = default)
    {
        var raw = await GetAsync(key, null, cancellationToken);
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : defaultValue;
    }

    public async Task<bool> GetBoolAsync(string key, bool defaultValue = false, CancellationToken cancellationToken = default)
    {
        var raw = await GetAsync(key, null, cancellationToken);
        return bool.TryParse(raw, out var b) ? b : defaultValue;
    }

    public async Task<T?> GetObjectAsync<T>(string key, T? defaultValue = default, CancellationToken cancellationToken = default) where T : class
    {
        var raw = await GetAsync(key, null, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
        try
        {
            return JsonSerializer.Deserialize<T>(raw, JsonOptions) ?? defaultValue;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Tenant setting {Key} is not valid JSON; returning default", key);
            return defaultValue;
        }
    }

    public async Task SetAsync(string key, string? value, string? description = null, bool isSecret = false, CancellationToken cancellationToken = default)
    {
        var repo = _uow.Repository<TenantSetting>();
        var existing = await repo.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (existing is null)
        {
            await repo.AddAsync(new TenantSetting
            {
                Key = key,
                Value = value,
                Description = description,
                IsSecret = isSecret,
                UpdatedAtUtc = _clock.UtcNow,
                UpdatedBy = _currentUser.UserId
            }, cancellationToken);
        }
        else
        {
            existing.Value = value;
            existing.Description = description ?? existing.Description;
            existing.IsSecret = isSecret;
            existing.UpdatedAtUtc = _clock.UtcNow;
            existing.UpdatedBy = _currentUser.UserId;
            repo.Update(existing);
        }
        await _uow.SaveChangesAsync(cancellationToken);
        await _cache.InvalidateTagAsync(CacheKeys.TenantSettingsTag, cancellationToken);
        _logger.LogInformation("Tenant setting updated: {Key} by {User}", key, _currentUser.UserId ?? "anonymous");
    }

    public Task InvalidateAsync(CancellationToken cancellationToken = default)
        => _cache.InvalidateTagAsync(CacheKeys.TenantSettingsTag, cancellationToken);

    // -------------------- helpers --------------------------------------------
    private Task<Dictionary<string, string?>> GetBagAsync(CancellationToken cancellationToken)
        => _cache.GetOrSetAsync(
            CacheKeys.TenantSettingsAll,
            async ct =>
            {
                // GroupBy(...).Select(g => g.Last(...)) is duplicate-safe — if a stale
                // row pair exists for the same (tenant, key), the most-recently-updated
                // wins. ToDictionary would throw on the duplicate which crashes startup.
                var rows = await _uow.Repository<TenantSetting>().ListAsync(ct);
                return rows
                    .GroupBy(r => r.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g
                        .OrderByDescending(r => r.UpdatedAtUtc)
                        .ThenByDescending(r => r.SettingId)
                        .First())
                    .OrderBy(s => s.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(r => r.Key, r => r.Value, StringComparer.OrdinalIgnoreCase);
            },
            ttl: CacheTtl.Long,
            tags: new[] { CacheKeys.TenantSettingsTag },
            cancellationToken: cancellationToken);
}
