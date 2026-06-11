// =============================================================================
// ErrorController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Renders a branded HTML page for every status code that bubbles out of the
// pipeline — wired via `app.UseStatusCodePagesWithReExecute("/error/{0}")` in
// Program.cs so 401 / 403 / 404 / 500 etc. all flow here.
//
// Each known status has its own view under /Views/Error/{code}.cshtml. Unknown
// codes fall back to Generic.cshtml which still renders the actual numeric code.
// =============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Presentation.Controllers;

[AllowAnonymous]
[Route("error")]
public sealed class ErrorController : Controller
{
    private static readonly HashSet<int> KnownCodes = new()
    {
        400, 401, 402, 403, 404, 405, 408, 410, 422, 429, 500, 502, 503, 504
    };

    [HttpGet("")]
    public IActionResult Index() => Show(500);

    [HttpGet("{code:int}")]
    public IActionResult Show(int code)
    {
        // Cap to a sensible range so a bot can't render /error/12345.
        if (code < 400 || code > 599) code = 500;

        Response.StatusCode = code;
        ViewData["TraceId"] = HttpContext.TraceIdentifier;

        // If this request was re-executed from a status-code page middleware,
        // surface the original path in the description for 404s etc.
        var feature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
        if (feature is not null)
        {
            ViewData["OriginalPath"] = feature.OriginalPath;
        }

        var viewName = KnownCodes.Contains(code) ? code.ToString() : "Generic";
        return View(viewName);
    }
}
