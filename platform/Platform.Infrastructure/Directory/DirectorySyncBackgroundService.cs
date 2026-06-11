// =============================================================================
// DirectorySyncBackgroundService  (Platform.Infrastructure.Directory)
// -----------------------------------------------------------------------------
// Hosted service that ticks every TickIntervalSeconds and asks the orchestrator
// to sync every active tenant that's past its scheduled cadence. Cadence is
// PER-TENANT via Directory.SyncIntervalMinutes (default 30 min); a healthy
// tenant only emits a real sync run every 30 min even though we tick more
// often.
//
// WHY tick frequently
//   Each tick is cheap: enumerate tenants, check LastSyncAtUtc, skip if not
//   due. The actual heavy work (DB / API call) only runs for due tenants. A
//   short tick interval keeps the cadence responsive when admins flip
//   Directory.SyncIntervalMinutes from 60 to 5.
//
// FAILURE ISOLATION
//   Per-tenant exceptions are caught inside IDirectorySyncService.SyncAllDueAsync
//   so one bad tenant can't take the whole loop down. The hosted service only
//   sees catastrophic / unexpected exceptions (DI scope failure, DbContext
//   disposal), which it logs + sleeps before retrying.
// =============================================================================

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.Application.Abstractions.Directory;

namespace Platform.Infrastructure.Directory;

public sealed class DirectorySyncBackgroundService : BackgroundService
{
    /// <summary>How often the outer loop wakes up. Inner per-tenant cadence is independent.</summary>
    public static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);

    /// <summary>Pause after a catastrophic loop error before retrying.</summary>
    public static readonly TimeSpan ErrorBackoff = TimeSpan.FromMinutes(5);

    private readonly IServiceProvider _sp;
    private readonly ILogger<DirectorySyncBackgroundService> _logger;

    public DirectorySyncBackgroundService(IServiceProvider sp, ILogger<DirectorySyncBackgroundService> logger)
    {
        _sp     = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Directory sync background service started; ticking every {Tick}.", TickInterval);

        // Give the app a moment to finish bootstrapping (migrations, seeds) before the first tick.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _sp.CreateAsyncScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<IDirectorySyncService>();
                var results = await orchestrator.SyncAllDueAsync(stoppingToken);

                if (results.Count > 0)
                {
                    _logger.LogInformation("Directory tick ran {N} tenant sync(s).", results.Count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Directory tick failed at the loop level; backing off {Backoff} before retry.", ErrorBackoff);
                try { await Task.Delay(ErrorBackoff, stoppingToken); }
                catch (OperationCanceledException) { break; }
                continue;
            }

            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("Directory sync background service stopped.");
    }
}
