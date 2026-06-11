namespace Platform.Domain.Tenancy;

/// Per-request tenant context. Resolved by middleware and consumed by the EF
/// query filter and by application services that need tenant scoping.
public interface ITenantContext
{
    int ClientId { get; }
    string ClientCode { get; }
    bool IsResolved { get; }
}
