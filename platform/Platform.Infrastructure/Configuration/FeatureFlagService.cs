// =============================================================================
// FeatureFlagService  (Platform.Infrastructure.Configuration)
// -----------------------------------------------------------------------------
// Thin wrapper over <see cref="ITenantSettings"/>. Reads booleans, writes
// booleans, exposes a bulk read for the admin UI.
// =============================================================================

using Platform.Application.Abstractions.Configuration;

namespace Platform.Infrastructure.Configuration;

public sealed class FeatureFlagService : IFeatureFlagService
{
    private readonly ITenantSettings _settings;

    public FeatureFlagService(ITenantSettings settings) { _settings = settings; }

    public Task<bool> IsEnabledAsync(string flag, CancellationToken ct = default)
        => _settings.GetBoolAsync(flag, defaultValue: true, ct);

    public Task<bool> IsEnabledAsync(string flag, bool defaultValue, CancellationToken ct = default)
        => _settings.GetBoolAsync(flag, defaultValue, ct);

    public Task SetAsync(string flag, bool enabled, CancellationToken ct = default)
        => _settings.SetAsync(flag, enabled ? "true" : "false", cancellationToken: ct);

    public async Task<IReadOnlyDictionary<string, bool>> GetWellKnownAsync(CancellationToken ct = default)
    {
        var dict = new Dictionary<string, bool>(FeatureFlags.All.Count);
        foreach (var f in FeatureFlags.All)
        {
            dict[f] = await _settings.GetBoolAsync(f, defaultValue: true, ct);
        }
        return dict;
    }
}
