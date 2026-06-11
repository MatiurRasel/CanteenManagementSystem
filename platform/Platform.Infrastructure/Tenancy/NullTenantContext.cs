using Platform.Application.Configuration;
using Platform.Domain.Tenancy;
using Microsoft.Extensions.Options;

namespace Platform.Infrastructure.Tenancy;

/// Fallback tenant context used when no per-request tenant has been resolved
/// (e.g. background jobs, design-time DbContext factory, startup seeder).
public sealed class NullTenantContext : ITenantContext
{
    private readonly TenancyOptions _options;

    public NullTenantContext(IOptions<TenancyOptions> options) => _options = options.Value;

    public int ClientId => _options.DefaultClientId;
    public string ClientCode => _options.DefaultClientCode;
    public bool IsResolved => false;
}
