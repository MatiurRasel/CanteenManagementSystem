using Platform.Application.Configuration;
using Microsoft.Extensions.Options;

namespace Platform.Presentation.Middleware.Tenancy;

/// Resolves tenant from the first subdomain of the host (e.g. "acme.canteen.example.com"
/// resolves to client code "acme"). Treats "www" and "localhost" as the default tenant.
public sealed class SubdomainTenantResolver : ITenantResolver
{
    private readonly TenancyOptions _options;

    public SubdomainTenantResolver(IOptions<TenancyOptions> options) => _options = options.Value;

    public Task<TenantIdentity?> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var host = httpContext.Request.Host.Host;
        if (string.IsNullOrEmpty(host)) return Task.FromResult<TenantIdentity?>(null);

        var parts = host.Split('.');
        if (parts.Length < 3) return Task.FromResult<TenantIdentity?>(null);

        var subdomain = parts[0];
        if (string.Equals(subdomain, "www", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(subdomain, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<TenantIdentity?>(null);
        }

        return Task.FromResult<TenantIdentity?>(new TenantIdentity(_options.DefaultClientId, subdomain));
    }
}
