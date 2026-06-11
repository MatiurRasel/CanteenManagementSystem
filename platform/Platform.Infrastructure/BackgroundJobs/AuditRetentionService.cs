// =============================================================================
// AuditRetentionService  (Platform.Infrastructure.BackgroundJobs)
// -----------------------------------------------------------------------------
// Periodically purges audit entries older than the per-tenant retention window.
//
// SCHEDULE
//   Wakes once every TickInterval (default: 24 h). On each wake:
//     1. Enumerate active tenants (cross-tenant — filter off).
//     2. For each tenant, open a scoped DI scope + stamp ITenantContext so the
//        EF global query filter restricts every subsequent query/delete to that
//        tenant's rows.
//     3. Read `Audit.RetentionDays` (default 365) from tenant settings.
//     4. ExecuteDeleteAsync to bulk-remove AuditEntry rows older than
//        UtcNow - retentionDays.
//
// TENANT CONFIG
//   Audit.RetentionDays           default 365   minimum 30 (safety floor)
//   Audit.RetentionEnabled        default true  set to "false" to pause purge for a tenant
//
// SAFETY
//   * Each tenant runs in isolation — one tenant's broken settings can't stop another.
//   * Minimum retention of 30 days is enforced even if the setting says less.
//   * Pre-DELETE count is logged so an operator can see "purged N rows for tenant T".
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Abstractions.Tenancy;
using Platform.Application.Persistence;
using Platform.Domain.Audit;
using Platform.Domain.Tenancy;

namespace Platform.Infrastructure.BackgroundJobs;

public sealed class AuditRetentionService : BackgroundService
{
    public static readonly TimeSpan TickInterval = TimeSpan.FromHours(24);
    public static readonly TimeSpan ErrorBackoff = TimeSpan.FromHours(1);
    public const int MinimumRetentionDays = 30;
    public const int DefaultRetentionDays = 365;

    private readonly IServiceProvider _sp;
    private readonly ILogger<AuditRetentionService> _logger;

    public AuditRetentionService(IServiceProvider sp, ILogger<AuditRetentionService> logger)
    {
        _sp = sp; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Audit retention service started; ticking every {Tick}.", TickInterval);
        // Give the host a few minutes to settle (migrations, seeds) before the first tick.
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgeAllTenantsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit retention sweep failed at the loop level; backing off {Backoff}.", ErrorBackoff);
                try { await Task.Delay(ErrorBackoff, stoppingToken); }
                catch (OperationCanceledException) { break; }
                continue;
            }

            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("Audit retention service stopped.");
    }

    private async Task PurgeAllTenantsAsync(CancellationToken ct)
    {
        // Cross-tenant enumeration — filter off.
        await using var rootScope = _sp.CreateAsyncScope();
        var clients = rootScope.ServiceProvider.GetRequiredService<IReadOnlyRepository<Client>>();
        var tenants = await clients.NoTrackingQuery()
            .IgnoreQueryFilters()
            .Where(c => c.IsActive)
            .Select(c => new { c.ClientId, c.ClientCode })
            .ToListAsync(ct);

        foreach (var t in tenants)
        {
            if (ct.IsCancellationRequested) break;
            await PurgeOneTenantAsync(t.ClientId, t.ClientCode, ct);
        }
    }

    private async Task PurgeOneTenantAsync(int clientId, string clientCode, CancellationToken ct)
    {
        try
        {
            await using var scope = _sp.CreateAsyncScope();
            var sp = scope.ServiceProvider;

            // Stamp the tenant context for this scope so the global query filter
            // restricts the upcoming DELETE to this tenant's audit rows only.
            if (sp.GetRequiredService<Platform.Domain.Tenancy.ITenantContext>() is IMutableTenantContext mutable)
            {
                mutable.Resolve(clientId, clientCode);
            }

            var settings = sp.GetRequiredService<ITenantSettings>();
            var enabled  = await settings.GetBoolAsync("Audit.RetentionEnabled", true, ct);
            if (!enabled)
            {
                _logger.LogDebug("Tenant {Tenant} has audit retention disabled — skipping.", clientCode);
                return;
            }

            var days = await settings.GetIntAsync("Audit.RetentionDays", DefaultRetentionDays, ct);
            days = Math.Max(MinimumRetentionDays, days);

            var cutoff = DateTime.UtcNow.AddDays(-days);

            var auditRepo = sp.GetRequiredService<IRepository<AuditEntry>>();
            var deleted = await auditRepo.Query()
                .Where(a => a.OccurredAtUtc < cutoff)
                .ExecuteDeleteAsync(ct);

            if (deleted > 0)
            {
                _logger.LogInformation(
                    "Audit retention: purged {Deleted} entries older than {Cutoff:u} for tenant {Tenant} (retention={Days}d).",
                    deleted, cutoff, clientCode, days);
            }
        }
        catch (Exception ex)
        {
            // One bad tenant must not stop the others — log and move on.
            _logger.LogError(ex, "Audit retention failed for tenant {Tenant}.", clientCode);
        }
    }
}
