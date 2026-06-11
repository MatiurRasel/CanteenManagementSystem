// =============================================================================
// IFeatureFlagService  (Platform.Application.Abstractions.Configuration)
// -----------------------------------------------------------------------------
// Tenant-scoped feature gates backed by TenantSetting rows. Each flag is one
// row keyed by <c>Feature.{flag}</c> with a boolean string value. The service
// is intentionally thin — the persistence backbone is the existing
// <see cref="ITenantSettings"/> bag with its L1 cache.
//
// USAGE
//   if (await _flags.IsEnabledAsync(FeatureFlags.KitchenDisplay)) { … }
//   await _flags.SetAsync(FeatureFlags.KitchenDisplay, true);
//
// CLI / ADMIN
//   /admin/feature-flags lists every well-known flag with a toggle. Custom
//   per-tenant flags (`Feature.Experiment.X`) are still readable via
//   ITenantSettings.GetBoolAsync directly.
// =============================================================================

namespace Platform.Application.Abstractions.Configuration;

public interface IFeatureFlagService
{
    Task<bool> IsEnabledAsync(string flag, CancellationToken ct = default);
    Task<bool> IsEnabledAsync(string flag, bool defaultValue, CancellationToken ct = default);
    Task SetAsync(string flag, bool enabled, CancellationToken ct = default);

    /// <summary>Read every well-known flag in one round trip — admin UI.</summary>
    Task<IReadOnlyDictionary<string, bool>> GetWellKnownAsync(CancellationToken ct = default);
}

/// <summary>Well-known feature flag keys. Add new entries here — they auto-appear on /admin/feature-flags.</summary>
public static class FeatureFlags
{
    public const string KitchenDisplay     = "Feature.KitchenDisplay";
    public const string SelfServiceKiosk   = "Feature.SelfServiceKiosk";
    public const string PreOrder           = "Feature.PreOrder";
    public const string TableOrder         = "Feature.TableOrder";
    public const string Loyalty            = "Feature.Loyalty";
    public const string DiscountEngine     = "Feature.DiscountEngine";
    public const string ParentPortal       = "Feature.ParentPortal";
    public const string NbrProduction      = "Feature.NbrProduction";
    public const string CustomerDisplay    = "Feature.CustomerDisplay";
    public const string Mfa                = "Feature.Mfa";
    public const string TenantImpersonate  = "Feature.TenantImpersonate";

    public static IReadOnlyList<string> All =>
    [
        KitchenDisplay, SelfServiceKiosk, PreOrder, TableOrder, Loyalty,
        DiscountEngine, ParentPortal, NbrProduction, CustomerDisplay, Mfa,
        TenantImpersonate
    ];
}
