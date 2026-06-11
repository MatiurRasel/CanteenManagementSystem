// =============================================================================
// TenantsAdminController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Per ADR 0004 the controller depends on ITenantAdminService — no IAppDbContext.
// Tenant impersonation cookie stays here (HTTP concern, not domain).
// =============================================================================

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Tenancy;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "SystemAdmin")]
[Route("admin/tenants")]
public sealed class TenantsAdminController : Controller
{
    private readonly ITenantAdminService _tenants;
    public TenantsAdminController(ITenantAdminService tenants) => _tenants = tenants;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _tenants.ListAsync(ct));

    [HttpGet("new")]
    public IActionResult New() => View(new NewTenantForm());

    public sealed class NewTenantForm
    {
        [Required, StringLength(50)]  public string ClientCode { get; set; } = string.Empty;
        [Required, StringLength(200)] public string ClientName { get; set; } = string.Empty;
        [StringLength(100)]           public string? ShortName { get; set; }
        [EmailAddress, StringLength(200)] public string? Email { get; set; }
        [StringLength(20)]            public string? PhoneNumber { get; set; }
        [StringLength(500)]           public string? Address { get; set; }
        [StringLength(200)]           public string? WebsiteUrl { get; set; }

        /// <summary>Source type chosen on step 2 — wires Directory.Source as a starting point.</summary>
        [Required] public string DirectorySource { get; set; } = "Manual";

        /// <summary>Cadence (min) for the background worker.</summary>
        [Range(5, 1440)] public int SyncIntervalMinutes { get; set; } = 30;

        [Required, EmailAddress, StringLength(200)]
        public string AdminEmail { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string AdminDisplayName { get; set; } = string.Empty;
    }

    [HttpPost("new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> New(NewTenantForm form, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(form);

        var result = await _tenants.CreateAsync(new NewTenantInput(
            form.ClientCode, form.ClientName, form.ShortName,
            form.Email, form.PhoneNumber, form.Address, form.WebsiteUrl,
            form.AdminEmail, form.AdminDisplayName), ct);

        if (!result.IsSuccess)
        {
            ModelState.AddModelError(nameof(form.ClientCode), result.Error.Message ?? "Could not create tenant.");
            return View(form);
        }

        var outcome = result.Value!;
        TempData["NewTenant.Code"] = outcome.Client.ClientCode;
        TempData["NewTenant.AdminLogin"] = outcome.AdminLogin;
        TempData["NewTenant.BootstrapPassword"] = outcome.BootstrapPassword;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, CancellationToken ct)
    {
        var result = await _tenants.ToggleActiveAsync(id, ct);
        if (!result.IsSuccess) return NotFound();
        var client = result.Value!;
        TempData["Flash.Success"] = $"Tenant {client.ClientCode} {(client.IsActive ? "re-enabled" : "suspended")}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/impersonate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Impersonate(int id, CancellationToken ct)
    {
        var client = await _tenants.GetByIdAsync(id, ct);
        if (client is null) return NotFound();

        Response.Cookies.Append("ccs.impersonate",
            $"{client.ClientId}|{client.ClientCode}",
            new Microsoft.AspNetCore.Http.CookieOptions
            {
                HttpOnly = true,
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                Secure   = Request.IsHttps,
                Path     = "/"
            });
        TempData["Flash.Info"] = $"Now acting as {client.ClientCode}. Use 'Stop impersonating' in the topbar to return.";
        return Redirect("/admin/directory");
    }

    [HttpPost("stop-impersonating")]
    [ValidateAntiForgeryToken]
    public IActionResult StopImpersonating()
    {
        Response.Cookies.Delete("ccs.impersonate");
        TempData["Flash.Success"] = "Impersonation ended.";
        return RedirectToAction(nameof(Index));
    }
}
