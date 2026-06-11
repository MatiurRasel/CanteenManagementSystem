namespace Platform.Presentation.Middleware.Tenancy;

/// Walks each registered resolver in priority order and returns the first hit.
public sealed class CompositeTenantResolver : ITenantResolver
{
    private readonly IReadOnlyList<ITenantResolver> _resolvers;

    public CompositeTenantResolver(IEnumerable<ITenantResolver> resolvers)
        => _resolvers = resolvers.ToList();

    public async Task<TenantIdentity?> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        foreach (var resolver in _resolvers)
        {
            var identity = await resolver.ResolveAsync(httpContext, cancellationToken);
            if (identity is not null) return identity;
        }
        return null;
    }
}
