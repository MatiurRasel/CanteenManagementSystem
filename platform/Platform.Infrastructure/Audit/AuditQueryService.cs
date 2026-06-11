// =============================================================================
// AuditQueryService  (Platform.Infrastructure.Audit)
// -----------------------------------------------------------------------------
// Default IAuditQueryService impl. Reads via IReadOnlyRepository<AuditEntry>
// — explicit read-only intent (ADR 0004 Batch 2).
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions.Audit;
using Platform.Application.Persistence;
using Platform.Domain.Audit;

namespace Platform.Infrastructure.Audit;

public sealed class AuditQueryService : IAuditQueryService
{
    private readonly IReadOnlyRepository<AuditEntry> _audit;
    public AuditQueryService(IReadOnlyRepository<AuditEntry> audit) => _audit = audit;

    public async Task<AuditPageResult> SearchAsync(AuditPageQuery q, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 10, 200);

        var qry = _audit.NoTrackingQuery()
            .Where(a => a.OccurredAtUtc >= q.FromUtc && a.OccurredAtUtc < q.ToUtcExclusive);

        if (!string.IsNullOrWhiteSpace(q.Action))     qry = qry.Where(a => a.Action == q.Action);
        if (!string.IsNullOrWhiteSpace(q.EntityType)) qry = qry.Where(a => a.EntityType == q.EntityType);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var needle = q.Search.Trim();
            qry = qry.Where(a => (a.PerformedBy != null && a.PerformedBy.Contains(needle))
                              || (a.EntityId   != null && a.EntityId.Contains(needle))
                              || a.Action.Contains(needle));
        }

        var total = await qry.CountAsync(cancellationToken);
        var rows  = await qry.OrderByDescending(a => a.OccurredAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        // Distinct action codes for the filter dropdown (last 30 days).
        var since = DateTime.UtcNow.AddDays(-30);
        var actions = await _audit.NoTrackingQuery()
            .Where(a => a.OccurredAtUtc >= since)
            .Select(a => a.Action).Distinct().OrderBy(a => a)
            .ToListAsync(cancellationToken);

        return new AuditPageResult(rows, total, actions);
    }
}
