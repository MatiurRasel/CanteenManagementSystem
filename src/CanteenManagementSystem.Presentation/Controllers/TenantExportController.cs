// =============================================================================
// TenantExportController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// GDPR / off-boarding endpoints.
//   GET  /admin/tenancy/export  -> downloads a ZIP archive of every row this
//                                  tenant owns. Counts logged in audit trail.
// =============================================================================

using CanteenManagementSystem.Application.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Audit;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "TenantAdmin")]
[Route("admin/tenancy")]
public sealed class TenantExportController : Controller
{
    private readonly ITenantDataExportService _exporter;
    private readonly IAuditTrail _audit;

    public TenantExportController(ITenantDataExportService exporter, IAuditTrail audit)
    {
        _exporter = exporter; _audit = audit;
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var archive = await _exporter.ExportAsync(ct);
        await _audit.RecordAsync(
            action: "Tenant.DataExported",
            entityType: "Tenant",
            entityId: null,
            payload: new { archive.FileName, sizeBytes = archive.Bytes.Length },
            cancellationToken: ct);
        return File(archive.Bytes, archive.ContentType, archive.FileName);
    }
}
