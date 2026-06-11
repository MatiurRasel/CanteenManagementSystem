// =============================================================================
// TenantLifecycleController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// SysAdmin-only soft-delete + restore endpoints. Hard-delete is automatic via
// the TenantHardDeleteService background sweep.
//
//   POST /sysadmin/tenants/{id}/soft-delete  — flips DeletedAtUtc + HoldUntilUtc
//   POST /sysadmin/tenants/{id}/restore      — undo within hold
//   POST /sysadmin/tenants/{id}/hard-delete  — emergency manual hard-delete
// =============================================================================

using CanteenManagementSystem.Application.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "SysAdmin")]
[Route("sysadmin/tenants")]
public sealed class TenantLifecycleController : Controller
{
    private readonly ITenantDeletionService _deletion;
    public TenantLifecycleController(ITenantDeletionService deletion) => _deletion = deletion;

    private string? Me() => User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpPost("{id:int}/soft-delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SoftDelete(int id, string reason, int? holdDays, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["Flash.Error"] = "Provide a reason for the soft-delete (audit).";
            return Redirect("/sysadmin/tenants");
        }
        var ok = await _deletion.SoftDeleteAsync(new SoftDeleteRequest(id, reason, holdDays, Me()), ct);
        TempData[ok ? "Flash.Success" : "Flash.Error"] = ok
            ? "Tenant soft-deleted. Hard-delete will run after the hold window."
            : "Tenant not found or already deleted.";
        return Redirect("/sysadmin/tenants");
    }

    [HttpPost("{id:int}/restore")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(int id, CancellationToken ct)
    {
        var ok = await _deletion.RestoreAsync(id, Me(), ct);
        TempData[ok ? "Flash.Success" : "Flash.Error"] = ok
            ? "Tenant restored."
            : "Tenant not found or not in soft-deleted state.";
        return Redirect("/sysadmin/tenants");
    }

    [HttpPost("{id:int}/hard-delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDelete(int id, string confirm, CancellationToken ct)
    {
        if (!string.Equals(confirm, "PERMANENTLY DELETE", StringComparison.Ordinal))
        {
            TempData["Flash.Error"] = "Type 'PERMANENTLY DELETE' to confirm.";
            return Redirect("/sysadmin/tenants");
        }
        var rows = await _deletion.HardDeleteAsync(id, Me(), ct);
        TempData["Flash.Success"] = $"Tenant hard-deleted ({rows} rows removed).";
        return Redirect("/sysadmin/tenants");
    }
}
