using Platform.Application.Abstractions.Tenancy;
using Platform.Domain.Tenancy;

namespace Platform.Presentation.Middleware.Tenancy;

/// Mutable per-request tenant context populated by TenantContextMiddleware
/// (web requests) and DirectorySyncService.SyncAllDueAsync (background loop).
/// Registered Scoped so each HTTP request / scope gets its own instance.
public sealed class RequestTenantContext : ITenantContext, IMutableTenantContext
{
    public int ClientId { get; private set; }
    public string ClientCode { get; private set; } = string.Empty;
    public bool IsResolved { get; private set; }

    public void Resolve(int clientId, string clientCode)
    {
        ClientId = clientId;
        ClientCode = clientCode;
        IsResolved = true;
    }
}
