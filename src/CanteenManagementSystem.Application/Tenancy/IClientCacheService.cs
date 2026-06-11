using Platform.Domain.Tenancy;

namespace CanteenManagementSystem.Application.Tenancy;

/// Cache facade for the current tenant directory (<see cref="Client"/>) plus
/// Azure-hosted photo URL composition. The Azure container path used to live
/// in UTClientSettings.UserVal — it now lives in tenant settings under the
/// key "Branding.PhotoContainer" (DB row -&gt; appsettings -&gt; empty).
public interface IClientCacheService
{
    /// <summary>Returns the current tenant. ALSO primes the photo-container cache for sync use.</summary>
    Task<Client> GetClientInfoAsync();

    /// <summary>Evicts both the tenant and the photo-container memory entries.</summary>
    Task RefreshCacheAsync();

    /// <summary>
    /// Compose an absolute photo URL. Sync because hot paths (per-row in lists)
    /// can't afford a DB hit. Reads the container path from MemoryCache — make
    /// sure <see cref="GetClientInfoAsync"/> has been called at least once
    /// per process (TenantContextMiddleware does this).
    /// </summary>
    string GetPhotoUrl(string? photoPath);
}
