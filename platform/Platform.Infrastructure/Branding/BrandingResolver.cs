// =============================================================================
// BrandingResolver  (Platform.Infrastructure.Branding)
// -----------------------------------------------------------------------------
// Default IBrandingResolver impl. Reads tenant-level overrides via
// ITenantSettings (cached L1+L2) and falls back to IOptions<BrandingConfiguration>
// for environment-wide defaults.
//
// PER-REQUEST CACHING
//   The resolver caches the resolved record in a single field for the lifetime
//   of the request — so the layout calling .AccentColor + .AppName + .LogoUrl
//   does ONE tenant-settings round-trip total (the underlying ITenantSettings
//   does L1 in-memory hit so even that is a microsecond).
// =============================================================================

using Microsoft.Extensions.Options;
using Platform.Application.Abstractions.Branding;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Configuration;

namespace Platform.Infrastructure.Branding;

public sealed class BrandingResolver : IBrandingResolver
{
    private readonly ITenantSettings _settings;
    private readonly BrandingConfiguration _defaults;
    private ResolvedBranding? _cached;

    public BrandingResolver(ITenantSettings settings, IOptions<BrandingConfiguration> defaults)
    {
        _settings = settings;
        _defaults = defaults.Value;
    }

    public async Task<ResolvedBranding> ResolveAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null) return _cached;

        async Task<string> R(string key, string fallback) =>
            (await _settings.GetAsync($"Branding.{key}", fallback, cancellationToken)) ?? fallback;

        _cached = new ResolvedBranding(
            AppName:          await R(nameof(BrandingConfiguration.AppName),         _defaults.AppName),
            AppSubtitle:      await R(nameof(BrandingConfiguration.AppSubtitle),     _defaults.AppSubtitle),
            AccentColor:      await R(nameof(BrandingConfiguration.AccentColor),     _defaults.AccentColor),
            SidebarColor:     await R(nameof(BrandingConfiguration.SidebarColor),    _defaults.SidebarColor),
            TopbarColor:      await R(nameof(BrandingConfiguration.TopbarColor),     _defaults.TopbarColor),
            MenuActiveColor:  await R(nameof(BrandingConfiguration.MenuActiveColor), _defaults.MenuActiveColor),
            MenuTextColor:    await R(nameof(BrandingConfiguration.MenuTextColor),   _defaults.MenuTextColor),
            SuccessColor:     await R(nameof(BrandingConfiguration.SuccessColor),    _defaults.SuccessColor),
            WarningColor:     await R(nameof(BrandingConfiguration.WarningColor),    _defaults.WarningColor),
            ErrorColor:       await R(nameof(BrandingConfiguration.ErrorColor),      _defaults.ErrorColor),
            InfoColor:        await R(nameof(BrandingConfiguration.InfoColor),       _defaults.InfoColor),
            LogoUrl:          await _settings.GetAsync("Branding.LogoUrl", null, cancellationToken));

        return _cached;
    }

    public void Invalidate() => _cached = null;
}
