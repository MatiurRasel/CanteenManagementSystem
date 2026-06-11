using Platform.Application.Configuration;
using Microsoft.Extensions.Options;

namespace Platform.Presentation.Middleware.Tenancy;

/// Resolves tenant from an "X-Tenant" HTTP header. First in the chain so
/// service-to-service callers and tests can force a specific tenant.
public sealed class HeaderTenantResolver : ITenantResolver
{
    private const string HeaderName = "X-Tenant";
    private readonly TenancyOptions _options;

    public HeaderTenantResolver(IOptions<TenancyOptions> options) => _options = options.Value;

    public Task<TenantIdentity?> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        if (!httpContext.Request.Headers.TryGetValue(HeaderName, out var header) || string.IsNullOrWhiteSpace(header))
        {
            return Task.FromResult<TenantIdentity?>(null);
        }

        var clientCode = header.ToString().Trim();
        return Task.FromResult<TenantIdentity?>(new TenantIdentity(_options.DefaultClientId, clientCode));
    }
}
