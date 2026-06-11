#nullable enable

using Platform.Application.Abstractions.Configuration;
using Platform.Application.Persistence;
using Platform.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CanteenManagementSystem.Application.Tenancy;

/// Caches the current tenant <see cref="Client"/> row and composes Azure-hosted
/// photo URLs. The Azure container path lives in tenant settings under
/// "Branding.PhotoContainer" — DB row first, then appsettings, then empty.
public class ClientCacheService : IClientCacheService
{
    private readonly IRepository<Client> _clients;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ITenantSettings _tenantSettings;
    private readonly ILogger<ClientCacheService> _logger;

    private const string CLIENT_KEY = "client:current";
    private const string PHOTO_CONTAINER_CACHE_KEY = "client:photo-container";
    private const string PHOTO_CONTAINER_SETTING_KEY = "Branding.PhotoContainer";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    public ClientCacheService(
        IRepository<Client> clients,
        IMemoryCache cache,
        IConfiguration configuration,
        ITenantSettings tenantSettings,
        ILogger<ClientCacheService> logger)
    {
        _clients = clients;
        _cache = cache;
        _configuration = configuration;
        _tenantSettings = tenantSettings;
        _logger = logger;
    }

    public async Task<Client> GetClientInfoAsync()
    {
        if (_cache.TryGetValue(CLIENT_KEY, out Client? cached) && cached is not null)
        {
            return cached;
        }

        var client = await _clients.Query()
            .AsNoTracking()
            .OrderBy(c => c.ClientId)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException(
                "No tenant row found in Clients table. The ClientsSeed should have inserted the default tenant — check seed logs.");

        _cache.Set(CLIENT_KEY, client, CacheDuration);

        // Prime the photo-container path so sync GetPhotoUrl works for the lifetime of this cache entry.
        var containerPath = (await _tenantSettings.GetAsync(PHOTO_CONTAINER_SETTING_KEY, defaultValue: string.Empty))?.TrimEnd('/') ?? string.Empty;
        _cache.Set(PHOTO_CONTAINER_CACHE_KEY, containerPath, CacheDuration);

        _logger.LogInformation("Cached tenant {ClientCode} ({ShortName}).", client.ClientCode, client.ShortName);
        return client;
    }

    public Task RefreshCacheAsync()
    {
        _cache.Remove(CLIENT_KEY);
        _cache.Remove(PHOTO_CONTAINER_CACHE_KEY);
        return Task.CompletedTask;
    }

    public string GetPhotoUrl(string? photoPath)
    {
        if (string.IsNullOrWhiteSpace(photoPath) || photoPath == "--" || photoPath == "-")
        {
            return "/images/default-avatar.png";
        }

        var azureUrl = _configuration["AzureStorageUrl"]?.TrimEnd('/');
        var containerPath = _cache.TryGetValue(PHOTO_CONTAINER_CACHE_KEY, out string? cached) ? cached : null;

        if (string.IsNullOrEmpty(azureUrl) || string.IsNullOrEmpty(containerPath))
        {
            return "/images/default-avatar.png";
        }

        return $"{azureUrl}/{containerPath}/{photoPath.TrimStart('/')}";
    }
}
