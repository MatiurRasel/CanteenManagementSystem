namespace Platform.Presentation.Middleware.Tenancy;

/// Strategy seam: resolves a tenant identity from the inbound request. Concrete
/// strategies chain inside <see cref="CompositeTenantResolver"/>.
public interface ITenantResolver
{
    Task<TenantIdentity?> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
}

public readonly record struct TenantIdentity(int ClientId, string ClientCode);
