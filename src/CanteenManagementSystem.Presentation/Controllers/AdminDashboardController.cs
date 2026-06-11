// =============================================================================
// AdminDashboardController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// /admin/dashboard — tenant-admin landing.
// Pulls a single snapshot via IAdminDashboardService so the controller stays
// thin (ADR 0004) and the read can be cached at the service layer later.
// =============================================================================

using CanteenManagementSystem.Application.Dashboards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "TenantAdmin")]
[Route("admin/dashboard")]
public sealed class AdminDashboardController : Controller
{
    private readonly IAdminDashboardService _dashboard;
    public AdminDashboardController(IAdminDashboardService dashboard) => _dashboard = dashboard;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _dashboard.GetSnapshotAsync(ct));
}
