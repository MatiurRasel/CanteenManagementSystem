// =============================================================================
// MustChangePasswordMiddleware  (Platform.Presentation.Middleware)
// -----------------------------------------------------------------------------
// When a user signed in with MustChangePassword=true the cookie carries the
// claim "must_change_password=true". This middleware redirects them to
// /Account/ChangePassword for every other route until they update — they
// can't reach admin pages, the counter, or any API endpoint in the meantime.
//
// EXEMPT ROUTES (so the change flow + sign-out work)
//   /Account/*  — login, change-password, logout, access denied
//   /css, /js, /lib, /images, /favicon.ico  — static assets the page itself needs
// =============================================================================

namespace Platform.Presentation.Middleware;

public sealed class MustChangePasswordMiddleware
{
    private readonly RequestDelegate _next;

    public MustChangePasswordMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated == true &&
            context.User.HasClaim("must_change_password", "true"))
        {
            var path = context.Request.Path.Value ?? string.Empty;
            if (!IsExempt(path))
            {
                context.Response.Redirect("/Account/ChangePassword");
                return;
            }
        }

        await _next(context);
    }

    private static bool IsExempt(string path)
        => path.StartsWith("/Account/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/css/",     StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/js/",      StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/lib/",     StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/images/",  StringComparison.OrdinalIgnoreCase)
        || path.Equals("/favicon.ico",  StringComparison.OrdinalIgnoreCase);
}
