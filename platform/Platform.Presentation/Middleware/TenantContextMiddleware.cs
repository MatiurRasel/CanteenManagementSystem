// =============================================================================
// TenantContextMiddleware  (Platform.Presentation.Middleware)
// -----------------------------------------------------------------------------
// Resolves the per-request tenant identity in priority order:
//
//   1. SystemAdmin impersonation cookie ("ccs.impersonate=ID|CODE")  HIGHEST
//      Allows the platform admin to drive any tenant's UI without re-logging
//      in. Ignored unless the user has the SystemAdmin role.
//
//   2. Authenticated user's claim ("client_id" / "tenant")
//      Set when the user signed in (cookie) or by the JWT issuer (Bearer).
//      Authoritative for normal users — overrides any subdomain trickery.
//
//   3. X-Tenant HTTP header     (HeaderTenantResolver)
//      Service-to-service callers without identity.
//
//   4. First-label subdomain    (SubdomainTenantResolver)
//
//   5. Configured default       (DefaultTenantResolver)
//
// MUST RUN AFTER UseAuthentication() so HttpContext.User is populated.
// =============================================================================

using System.Security.Claims;
using Platform.Application.Abstractions.Tenancy;
using Platform.Domain.Tenancy;
using Platform.Presentation.Middleware.Tenancy;

namespace Platform.Presentation.Middleware;

public sealed class TenantContextMiddleware
{
    private readonly RequestDelegate _next;

    public TenantContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantResolver resolver, ITenantContext tenant)
    {
        if (tenant is not IMutableTenantContext mutable)
        {
            await _next(context);
            return;
        }

        // 1. SystemAdmin impersonation cookie wins.
        if (context.User?.Identity?.IsAuthenticated == true &&
            context.User.IsInRole("SystemAdmin") &&
            context.Request.Cookies.TryGetValue("ccs.impersonate", out var imp) &&
            !string.IsNullOrEmpty(imp))
        {
            var parts = imp.Split('|', 2);
            if (parts.Length == 2 && int.TryParse(parts[0], out var impId) && impId > 0)
            {
                mutable.Resolve(impId, parts[1]);
                await _next(context);
                return;
            }
        }

        // 2. Authenticated-user claim.
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var clientIdRaw = context.User.FindFirstValue("client_id");
            var tenantCode  = context.User.FindFirstValue("tenant");
            if (int.TryParse(clientIdRaw, out var clientId) && clientId > 0 && !string.IsNullOrWhiteSpace(tenantCode))
            {
                mutable.Resolve(clientId, tenantCode!);
                await _next(context);
                return;
            }
        }

        // 3-5. Header / Subdomain / Default chain.
        var identity = await resolver.ResolveAsync(context, context.RequestAborted);
        if (identity is not null)
        {
            mutable.Resolve(identity.Value.ClientId, identity.Value.ClientCode);
        }

        await _next(context);
    }
}
