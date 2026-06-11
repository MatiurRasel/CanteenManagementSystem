// =============================================================================
// IMutableTenantContext  (Platform.Application.Abstractions.Tenancy)
// -----------------------------------------------------------------------------
// Lets out-of-request code (background workers, hosted services, seeders)
// stamp a tenant onto the current DI scope. Web requests go through
// TenantContextMiddleware which calls the same Resolve method — by exposing it
// in Application we avoid lower layers having to depend on Presentation.
//
// The CONCRETE impl is RequestTenantContext (Platform.Presentation.Middleware).
// Workers that need to switch tenants: CreateScope() → cast ITenantContext to
// IMutableTenantContext → Resolve(clientId, clientCode).
// =============================================================================

using Platform.Domain.Tenancy;

namespace Platform.Application.Abstractions.Tenancy;

public interface IMutableTenantContext : ITenantContext
{
    void Resolve(int clientId, string clientCode);
}
