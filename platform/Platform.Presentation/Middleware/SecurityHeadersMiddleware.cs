// =============================================================================
// SecurityHeadersMiddleware  (Platform.Presentation.Middleware)
// -----------------------------------------------------------------------------
// One stop for the security-related response headers a modern browser expects.
// Adds them BEFORE static-file serving so 304-not-modified responses also get
// the protection.
//
// Headers set:
//   * Content-Security-Policy
//       Per-request nonce (24 bytes / base64) attached to HttpContext.Items
//       under the key "csp-nonce". Razor pages emit the nonce via the
//       <script>/<style> auto-tag-helper at Platform.Presentation.Security.
//       Modern browsers prefer the nonce; 'unsafe-inline' is kept as a
//       fallback for the Sneat theme's inline event handlers (onclick=...)
//       which CSP3 cannot guard with nonces. When Security:CspStrict=true
//       the fallback is dropped on /admin and /api paths — those surfaces
//       do not rely on the theme's inline attrs.
//   * Strict-Transport-Security                  HSTS (HTTPS only)
//   * X-Content-Type-Options                     no MIME sniffing
//   * X-Frame-Options                            click-jacking protection
//   * Referrer-Policy                            strict-origin-when-cross-origin
//   * Permissions-Policy                         disable risky surfaces
//   * X-Robots-Tag                               no indexing on /admin / /api
//
// Tenant-customisable: pass overrides via Security:* tenant settings later.
// =============================================================================

using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;

namespace Platform.Presentation.Middleware;

public sealed class SecurityHeadersMiddleware
{
    /// <summary>Key under which the per-request CSP nonce is stored on HttpContext.Items.</summary>
    public const string CspNonceItemKey = "csp-nonce";

    private readonly RequestDelegate _next;
    private readonly bool _isDevelopment;
    private readonly bool _strictCsp;

    public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment env, IConfiguration configuration)
    {
        _next = next;
        _isDevelopment = env.IsDevelopment();
        _strictCsp = configuration.GetValue("Security:CspStrict", false);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // ── Generate a per-request nonce and stash it for Razor / tag helpers ──
        var nonceBytes = RandomNumberGenerator.GetBytes(24);
        var nonce = Convert.ToBase64String(nonceBytes);
        context.Items[CspNonceItemKey] = nonce;

        var headers = context.Response.Headers;

        // Content-Security-Policy — uses the CSP-Level-3 split directives so we
        // can have STRICT nonce-only enforcement on <script>/<style> ELEMENTS
        // while still allowing inline style="..." attributes that the Sneat
        // theme depends on. Per CSP3, when a nonce is present in a -src list,
        // 'unsafe-inline' is IGNORED for that list — which is why we cannot
        // mix them. The -elem / -attr split is the spec-blessed escape valve:
        //
        //   *-src-elem 'self' 'nonce-X' …    ← controls <script>/<style> blocks
        //   *-src-attr 'unsafe-inline'       ← controls onclick="…"/style="…"
        //
        // Strict mode (Security:CspStrict=true) on /admin + /api drops the
        // inline-attr escape valve since those surfaces don't rely on the
        // theme's inline event handlers.
        if (!headers.ContainsKey("Content-Security-Policy"))
        {
            var path = context.Request.Path.Value ?? string.Empty;
            var isStrictPath = _strictCsp && (
                path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("/api", StringComparison.OrdinalIgnoreCase));

            // All 3rd-party libraries (Boxicons / Flatpickr / SweetAlert2 / Bootstrap /
            // jQuery / Font Awesome) are vendored under /lib so we don't need any
            // CDN allowlist. SweetAlert2 injects a <style> tag at runtime — covered
            // by 'unsafe-inline' (modern browsers ignore it when a nonce is also
            // present, but the fall-through keeps the 3rd-party widget working).
            var scriptSrcElem = $"script-src-elem 'self' 'nonce-{nonce}'";
            var scriptSrcAttr = isStrictPath
                ? "script-src-attr 'none'"
                : "script-src-attr 'unsafe-inline'";

            var styleSrcElem = $"style-src-elem 'self' 'nonce-{nonce}' 'unsafe-inline' https://fonts.googleapis.com";
            var styleSrcAttr = isStrictPath
                ? "style-src-attr 'none'"
                : "style-src-attr 'unsafe-inline'";

            headers["Content-Security-Policy"] = string.Join("; ", new[]
            {
                "default-src 'self'",
                "img-src 'self' data: blob: https://*",
                styleSrcElem,
                styleSrcAttr,
                "font-src 'self' data: https://fonts.gstatic.com",
                scriptSrcElem,
                scriptSrcAttr,
                "connect-src 'self' ws: wss: https:",
                "frame-ancestors 'none'",
                "base-uri 'self'",
                "form-action 'self'",
                "object-src 'none'"
            });
        }

        // HSTS — only when the actual request is HTTPS. Production should still
        // enable UseHsts() in Program.cs for the dedicated HSTS preload.
        if (!_isDevelopment && context.Request.IsHttps && !headers.ContainsKey("Strict-Transport-Security"))
        {
            headers["Strict-Transport-Security"] = "max-age=63072000; includeSubDomains";   // 2 years
        }

        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"]        = "DENY";
        headers["Referrer-Policy"]        = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"]     = "geolocation=(), microphone=(), camera=(), payment=(self), usb=()";

        // No indexing for admin / API surfaces.
        var requestPath = context.Request.Path.Value ?? string.Empty;
        if (requestPath.StartsWith("/admin", StringComparison.OrdinalIgnoreCase) ||
            requestPath.StartsWith("/api",   StringComparison.OrdinalIgnoreCase))
        {
            headers["X-Robots-Tag"] = "noindex, nofollow";
        }

        await _next(context);
    }
}
