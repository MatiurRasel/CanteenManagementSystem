// =============================================================================
// DirectoryHealthCheck  (Presentation.HealthChecks)
// -----------------------------------------------------------------------------
// Reports the directory subsystem's health to the /health/ready endpoint.
// Aggregates across tenants:
//   * "Healthy"   → no tenant in Failing state
//   * "Degraded"  → at least one tenant in Failing state
//   * "Unhealthy" → seed/DB not initialised (cannot read Clients table)
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Abstractions.Directory;
using Platform.Application.Abstractions.Tenancy;
using Platform.Application.Persistence;
using Platform.Domain.Tenancy;

namespace CanteenManagementSystem.Presentation.HealthChecks;

public sealed class DirectoryHealthCheck : IHealthCheck
{
    private readonly IServiceProvider _sp;

    public DirectoryHealthCheck(IServiceProvider sp) => _sp = sp;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var scope = _sp.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

            var tenants = await db.Set<Client>().IgnoreQueryFilters().AsNoTracking()
                .Where(c => c.IsActive)
                .Select(c => new { c.ClientId, c.ClientCode })
                .ToListAsync(cancellationToken);

            if (tenants.Count == 0)
            {
                return HealthCheckResult.Healthy("no active tenants", new Dictionary<string, object> { ["tenantCount"] = 0 });
            }

            var failing = 0;
            foreach (var t in tenants)
            {
                await using var tenantScope = _sp.CreateAsyncScope();
                if (tenantScope.ServiceProvider.GetRequiredService<ITenantContext>() is IMutableTenantContext mut)
                    mut.Resolve(t.ClientId, t.ClientCode);
                var settings = tenantScope.ServiceProvider.GetRequiredService<ITenantSettings>();
                var status = await settings.GetAsync(DirectorySettingsKeys.HealthStatus, "Healthy", cancellationToken);
                if (string.Equals(status, "Failing", StringComparison.OrdinalIgnoreCase)) failing++;
            }

            var data = new Dictionary<string, object> { ["tenantCount"] = tenants.Count, ["failing"] = failing };
            return failing == 0
                ? HealthCheckResult.Healthy("all tenants healthy", data)
                : HealthCheckResult.Degraded($"{failing} tenant(s) failing", data: data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("directory subsystem unreachable", ex);
        }
    }
}
