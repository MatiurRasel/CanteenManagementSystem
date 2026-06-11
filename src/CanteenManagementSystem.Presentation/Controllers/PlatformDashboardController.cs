// =============================================================================
// PlatformDashboardController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// GET /sysadmin/dashboard — cross-tenant KPI dashboard for the platform owner.
// Gated by the SystemAdmin policy + admin IP allow-list middleware.
// =============================================================================

using CanteenManagementSystem.Application.Sysadmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "SystemAdmin")]
[Route("sysadmin/dashboard")]
public sealed class PlatformDashboardController : Controller
{
    private readonly IPlatformDashboardService _service;
    public PlatformDashboardController(IPlatformDashboardService service) => _service = service;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _service.GetSnapshotAsync(ct));
}
