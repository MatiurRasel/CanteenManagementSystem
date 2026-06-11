// =============================================================================
// ITenantSettings  (Application abstraction)
// -----------------------------------------------------------------------------
// The single seam for reading any configurable value. Concrete impl in
// Infrastructure caches DB rows in L1 (memory) and L2 (Redis) so the hot
// path is a microsecond dictionary hit, not a DB round-trip.
//
// USAGE EXAMPLES
//   var key   = await tenantSettings.GetAsync("Payments.Bkash.AppKey");
//   var rate  = await tenantSettings.GetDecimalAsync("VAT.Rate", defaultValue: 0m);
//   var senderId = await tenantSettings.GetAsync("Sms.SenderId", "CANTEEN");
//
// SETTING-EDITING FLOW
//   1. Admin UI POSTs to /api/v1/tenant-settings  (TODO: admin module)
//   2. SettingsService writes the row + bumps the tenant cache tag.
//   3. ITenantSettings.GetAsync misses cache on next read -> reloads.
//
// ENCRYPTION
//   When IsSecret=true, the value is run through IDataProtector before the
//   row is persisted so production DB dumps don't leak API keys. The reader
//   transparently decrypts on the way out.
// =============================================================================

namespace Platform.Application.Abstractions.Configuration;

public interface ITenantSettings
{
    /// <summary>Read a string value. Falls back to appsettings.json key, then defaultValue.</summary>
    Task<string?> GetAsync(string key, string? defaultValue = null, CancellationToken cancellationToken = default);

    Task<int>     GetIntAsync(string key, int defaultValue = 0, CancellationToken cancellationToken = default);
    Task<decimal> GetDecimalAsync(string key, decimal defaultValue = 0m, CancellationToken cancellationToken = default);
    Task<bool>    GetBoolAsync(string key, bool defaultValue = false, CancellationToken cancellationToken = default);
    Task<T?>      GetObjectAsync<T>(string key, T? defaultValue = default, CancellationToken cancellationToken = default) where T : class;

    /// <summary>Upsert a single key. Bumps the tenant cache tag so all readers refresh on next access.</summary>
    Task SetAsync(string key, string? value, string? description = null, bool isSecret = false, CancellationToken cancellationToken = default);

    /// <summary>Force-evict the cached settings for the current tenant. Useful after bulk import.</summary>
    Task InvalidateAsync(CancellationToken cancellationToken = default);
}
