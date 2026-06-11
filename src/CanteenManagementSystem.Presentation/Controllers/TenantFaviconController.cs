// =============================================================================
// TenantFaviconController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Serves the tenant-overridden favicon.
//   GET /favicon.ico  → 302 to `Branding.FaviconUrl` if configured,
//                       otherwise streams the static `wwwroot/favicon.ico` file.
//
// Wired with a route override in Program.cs so it intercepts the well-known
// /favicon.ico path before UseStaticFiles.
// =============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Platform.Application.Abstractions.Configuration;

namespace CanteenManagementSystem.Presentation.Controllers;

[AllowAnonymous]
public sealed class TenantFaviconController : Controller
{
    private readonly ITenantSettings _settings;
    private readonly IHostEnvironment _env;

    public TenantFaviconController(ITenantSettings settings, IHostEnvironment env)
    {
        _settings = settings; _env = env;
    }

    [Route("favicon.ico")]
    [Route("favicon.png")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var url = await _settings.GetAsync("Branding.FaviconUrl", defaultValue: null, ct);
        if (!string.IsNullOrWhiteSpace(url))
        {
            // External URL — redirect. CDN or per-tenant blob storage URLs work here.
            return Redirect(url);
        }

        // Fall back to the static file shipped with the app.
        var path = Path.Combine(_env.ContentRootPath, "wwwroot", "favicon.ico");
        if (System.IO.File.Exists(path))
        {
            return PhysicalFile(path, "image/x-icon");
        }
        return NotFound();
    }
}
