// =============================================================================
// IDirectorySourceFactory  (Platform.Application.Abstractions.Directory)
// -----------------------------------------------------------------------------
// Strategy resolver. Reads "Directory.Source" from the CURRENT tenant context
// and instantiates the matching IDirectorySource impl. Lives in Application
// so neither the orchestrator nor the background worker know the concrete
// source types.
//
// IMPORTANT — tenant scope
//   This factory MUST be called while the per-request scope already has
//   ITenantContext.ClientId set. The background worker enters a fresh DI
//   scope per tenant and stamps the tenant before resolving the factory.
// =============================================================================

namespace Platform.Application.Abstractions.Directory;

public interface IDirectorySourceFactory
{
    /// <summary>
    /// Returns the source for the current tenant, or NULL if Source="Manual"
    /// (the CSV upload path doesn't need a pull source) or Source="Disabled".
    /// </summary>
    Task<IDirectorySource?> ResolveCurrentTenantAsync(CancellationToken cancellationToken = default);
}
