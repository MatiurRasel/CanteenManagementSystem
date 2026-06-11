// =============================================================================
// IBrandingResolver  (Platform.Application.Abstractions.Branding)
// -----------------------------------------------------------------------------
// Layered branding resolution. Reads a single branding key in this order:
//
//   1. TenantSetting row  ("Branding.AccentColor"   per-tenant override)
//   2. appsettings.json   (IOptions<BrandingConfiguration>, env-wide default)
//   3. Hard-coded default (BrandingConfiguration property initialiser)
//
// The resolver caches per-request so a layout that reads .AccentColor +
// .AppName + .LogoUrl does one DB round-trip total.
//
// USED BY
//   * _Layout.cshtml         → renders the chrome with resolved values.
//   * /admin/branding        → admin form that writes tenant-level overrides.
//   * Template customizer    → per-user override sits on top of these.
// =============================================================================

namespace Platform.Application.Abstractions.Branding;

public interface IBrandingResolver
{
    Task<ResolvedBranding> ResolveAsync(CancellationToken cancellationToken = default);

    /// <summary>Invalidate the per-request cache. Called after admin writes a Branding.* setting.</summary>
    void Invalidate();
}

public sealed record ResolvedBranding(
    string AppName,
    string AppSubtitle,
    string AccentColor,
    string SidebarColor,
    string TopbarColor,
    string MenuActiveColor,
    string MenuTextColor,
    string SuccessColor,
    string WarningColor,
    string ErrorColor,
    string InfoColor,
    string? LogoUrl);
