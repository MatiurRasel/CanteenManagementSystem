// =============================================================================
// AuditTrail  (Platform.Infrastructure.Audit)
// -----------------------------------------------------------------------------
// IAuditTrail implementation — append-only writer for the AuditEntry table.
//
// LAYERING (ADR 0004, Batch 2)
//   Depends on IRepository<AuditEntry>, NOT IAppDbContext. The repository
//   wraps the DbContext; SaveChanges is the responsibility of the surrounding
//   command handler's UnitOfWork via the TransactionBehavior in the dispatcher
//   pipeline (so audit + business write commit atomically).
// =============================================================================

using System.Text.Json;
using Platform.Application.Persistence;
using Platform.Application.Abstractions.Audit;
using Platform.Application.Abstractions.Identity;
using Platform.Application.Abstractions.Time;
using Platform.Domain.Audit;
using Microsoft.AspNetCore.Http;

namespace Platform.Infrastructure.Audit;

public sealed class AuditTrail : IAuditTrail
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IRepository<AuditEntry> _repo;
    private readonly IClock _clock;
    private readonly ICurrentUser _currentUser;
    private readonly IHttpContextAccessor _httpContext;

    public AuditTrail(IRepository<AuditEntry> repo, IClock clock, ICurrentUser currentUser, IHttpContextAccessor httpContext)
    {
        _repo = repo;
        _clock = clock;
        _currentUser = currentUser;
        _httpContext = httpContext;
    }

    public async Task RecordAsync(string action, string? entityType = null, string? entityId = null, object? payload = null, CancellationToken cancellationToken = default)
    {
        var entry = new AuditEntry
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            PerformedBy = _currentUser.UserId,
            PerformedByRole = _currentUser.Role,
            PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload, _jsonOptions),
            IpAddress = _httpContext.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            CorrelationId = _httpContext.HttpContext?.TraceIdentifier,
            OccurredAtUtc = _clock.UtcNow
        };

        await _repo.AddAsync(entry, cancellationToken);
        // SaveChanges is intentionally NOT called here — the surrounding command
        // handler commits the audit row alongside its business write.
    }
}
