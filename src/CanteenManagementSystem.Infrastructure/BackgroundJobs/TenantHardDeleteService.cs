// =============================================================================
// TenantHardDeleteService  (CanteenManagementSystem.Infrastructure.BackgroundJobs)
// -----------------------------------------------------------------------------
// Scans for tenants whose HoldUntilUtc has passed and triggers
// ITenantDeletionService.HardDeleteAsync. Runs once an hour (mostly a no-op).
// =============================================================================

using CanteenManagementSystem.Application.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.Application.Persistence;
using Platform.Domain.Tenancy;

namespace CanteenManagementSystem.Infrastructure.BackgroundJobs;

public sealed class TenantHardDeleteService : BackgroundService
{
    public static readonly TimeSpan TickInterval = TimeSpan.FromHours(1);
    public static readonly TimeSpan ErrorBackoff = TimeSpan.FromHours(2);

    private readonly IServiceProvider _sp;
    private readonly ILogger<TenantHardDeleteService> _logger;

    public TenantHardDeleteService(IServiceProvider sp, ILogger<TenantHardDeleteService> logger)
    {
        _sp = sp; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Tenant hard-delete sweeper started; tick {Tick}.", TickInterval);
        try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await SweepAsync(stoppingToken); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hard-delete sweep failed; backing off {Backoff}.", ErrorBackoff);
                try { await Task.Delay(ErrorBackoff, stoppingToken); }
                catch (OperationCanceledException) { break; }
                continue;
            }
            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        await using var scope = _sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var deletion = scope.ServiceProvider.GetRequiredService<ITenantDeletionService>();

        var now = DateTime.UtcNow;
        var due = await db.Set<Client>().IgnoreQueryFilters()
            .Where(c => c.DeletedAtUtc != null && c.HoldUntilUtc != null && c.HoldUntilUtc < now)
            .Select(c => new { c.ClientId, c.ClientCode })
            .ToListAsync(ct);

        foreach (var c in due)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var n = await deletion.HardDeleteAsync(c.ClientId, performedBy: "system:hold-expired", ct);
                _logger.LogInformation("Hard-deleted tenant {Tenant} — {Rows} rows.", c.ClientCode, n);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hard-delete failed for tenant {Tenant}; will retry next tick.", c.ClientCode);
            }
        }
    }
}
