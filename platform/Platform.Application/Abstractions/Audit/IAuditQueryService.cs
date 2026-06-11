// =============================================================================
// IAuditQueryService  (Platform.Application.Abstractions.Audit)
// -----------------------------------------------------------------------------
// Read-only surface over the audit log. Used by /admin/audit so the controller
// never touches IAppDbContext directly (ADR 0004). Tenant scoping is enforced
// by the EF global query filter inside the repository.
// =============================================================================

using Platform.Domain.Audit;

namespace Platform.Application.Abstractions.Audit;

public sealed record AuditPageResult(
    IReadOnlyList<AuditEntry> Items,
    int TotalCount,
    IReadOnlyList<string> KnownActions);

public sealed record AuditPageQuery(
    DateTime FromUtc, DateTime ToUtcExclusive,
    string? Action, string? EntityType, string? Search,
    int Page, int PageSize);

public interface IAuditQueryService
{
    Task<AuditPageResult> SearchAsync(AuditPageQuery query, CancellationToken cancellationToken = default);
}
