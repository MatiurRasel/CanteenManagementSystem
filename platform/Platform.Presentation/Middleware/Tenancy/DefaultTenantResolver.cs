using Platform.Application.Configuration;
using Microsoft.Extensions.Options;

namespace Platform.Presentation.Middleware.Tenancy;

/// Terminal fallback. Always resolves to the configured default tenant so the
/// system stays operational in single-tenant deployments.
public sealed class DefaultTenantResolver : ITenantResolver
{
    private readonly TenancyOptions _options;

    public DefaultTenantResolver(IOptions<TenancyOptions> options) => _options = options.Value;

    public Task<TenantIdentity?> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
        => Task.FromResult<TenantIdentity?>(new TenantIdentity(_options.DefaultClientId, _options.DefaultClientCode));
}
