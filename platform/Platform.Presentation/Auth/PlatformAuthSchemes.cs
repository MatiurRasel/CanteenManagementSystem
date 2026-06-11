// =============================================================================
// PlatformAuthSchemes
// -----------------------------------------------------------------------------
// Three schemes, registered side-by-side:
//
//   "PlatformCookie" (DEFAULT for browser)   - admin UI, operator screens
//   "Bearer"         (JWT)                   - SPA / mobile API clients
//   "ApiKey"         (X-App-Key + signature) - service-to-service integrators
//
// Policies (SystemAdmin / TenantAdmin / Operator / Auditor) are registered to
// accept ALL THREE — so the same [Authorize(Policy = "TenantAdmin")] works
// whether the caller arrived with a session cookie, a Bearer JWT, or an
// HMAC-signed API key.
//
// USAGE
//   [Authorize(Policy = "TenantAdmin")]                    // any scheme
//   [Authorize(AuthenticationSchemes = "Bearer")]          // pin to one
// =============================================================================

namespace Platform.Presentation.Auth;

public static class PlatformAuthSchemes
{
    public const string Cookie   = "PlatformCookie";
    public const string Bearer   = "Bearer";
    public const string ApiKey   = "ApiKey";
    /// <summary>Short-lived holder cookie used to bridge from Google OAuth back to our app's cookie.</summary>
    public const string External = "External";
}
