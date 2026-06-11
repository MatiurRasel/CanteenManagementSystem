// =============================================================================
// DesignSystemController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// /design-system — one-page catalogue of the shadcn-flavoured tokens +
// components. Use it as the visual reference when building new screens, and
// as a smoke test after CSS / partial changes.
//
// Authorize at TenantAdmin so the page doesn't leak our internal vocabulary
// to anonymous visitors.
// =============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "TenantAdmin")]
[Route("design-system")]
public sealed class DesignSystemController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}
