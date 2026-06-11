// =============================================================================
// AuditAdminController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Read-only viewer over CanteenAuditEntries. Per ADR 0004 the controller now
// depends on IAuditQueryService — no IAppDbContext.
// =============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Audit;
using Platform.Domain.Audit;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "Auditor")]
[Route("admin/audit")]
public sealed class AuditAdminController : Controller
{
    private readonly IAuditQueryService _audit;
    public AuditAdminController(IAuditQueryService audit) => _audit = audit;

    public sealed record AuditPage(
        IReadOnlyList<AuditEntry> Items, int Page, int PageSize, int TotalCount,
        DateTime From, DateTime To, string? Action, string? EntityType, string? Q,
        IReadOnlyList<string> KnownActions);

    [HttpGet("")]
    public async Task<IActionResult> Index(
        DateTime? from = null, DateTime? to = null,
        string? action = null, string? entityType = null, string? q = null,
        int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var fromUtc = (from ?? DateTime.UtcNow.AddDays(-1)).Date;
        var toUtc   = (to   ?? DateTime.UtcNow).Date.AddDays(1);

        var result = await _audit.SearchAsync(
            new AuditPageQuery(fromUtc, toUtc, action, entityType, q, page, pageSize), ct);

        return View(new AuditPage(
            result.Items, page, pageSize, result.TotalCount,
            fromUtc, toUtc.AddDays(-1), action, entityType, q, result.KnownActions));
    }
}
