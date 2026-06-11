// =============================================================================
// GatewaysAdminController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Per ADR 0004 the controller depends on IGatewayAdminService — no IAppDbContext.
// The secret-aware-save semantics live in the service.
// =============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Payments;
using Platform.Domain.Payments;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "TenantAdmin")]
[Route("admin/gateways")]
public sealed class GatewaysAdminController : Controller
{
    private const string SecretMask = "***";
    private readonly IGatewayAdminService _gateways;
    public GatewaysAdminController(IGatewayAdminService gateways) => _gateways = gateways;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _gateways.ListAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var row = await _gateways.GetByIdAsync(id, ct);
        return row is null ? NotFound() : View(row);
    }

    [HttpPost("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PaymentGatewayConfig form, CancellationToken ct)
    {
        var result = await _gateways.SaveAsync(id, form, SecretMask, User.Identity?.Name, ct);
        if (!result.IsSuccess) return NotFound();
        TempData["Flash.Success"] = $"Gateway {result.Value!.GatewayCode} saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, CancellationToken ct)
    {
        var result = await _gateways.ToggleEnabledAsync(id, ct);
        if (!result.IsSuccess) return NotFound();
        var row = result.Value!;
        TempData["Flash.Success"] = $"{row.GatewayCode} {(row.IsEnabled ? "enabled" : "disabled")}.";
        return RedirectToAction(nameof(Index));
    }
}
