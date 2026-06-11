namespace Platform.Application.Abstractions.Audit;

/// Append-only audit sink. Every command handler that mutates sensitive state
/// (wallet, order, menu, tenant config) should record an audit entry.
public interface IAuditTrail
{
    Task RecordAsync(
        string action,
        string? entityType = null,
        string? entityId = null,
        object? payload = null,
        CancellationToken cancellationToken = default);
}
