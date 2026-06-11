// =============================================================================
// NotificationDispatcherService  (Infrastructure.BackgroundJobs)
// -----------------------------------------------------------------------------
// Background loop that picks up Queued NotificationLog rows and dispatches
// them through the matching INotificationChannel. Each attempt records a
// retry counter; a row that fails 3 times is set to Failed with the last
// error so support can triage.
//
// CADENCE: every 2 seconds when busy, 10 seconds when idle (adaptive sleep).
//
// SCALING NOTES
//   For higher throughput, replace the polling loop with a message broker
//   (Azure Service Bus, RabbitMQ). The seam already exists — just replace
//   this hosted service with a queue consumer that reads the same logs.
// =============================================================================

using Platform.Application.Persistence;
using Platform.Application.Abstractions.Notifications;
using Platform.Application.Abstractions.Time;
using Platform.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Platform.Infrastructure.BackgroundJobs;

public sealed class NotificationDispatcherService : BackgroundService
{
    private const int BatchSize = 25;
    private const int MaxAttempts = 3;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan BusyDelay = TimeSpan.FromSeconds(2);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationDispatcherService> _logger;

    public NotificationDispatcherService(IServiceScopeFactory scopeFactory, ILogger<NotificationDispatcherService> logger)
    {
        _scopeFactory = scopeFactory; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            int processed = 0;
            try { processed = await DrainAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "Notification dispatcher loop failed"); }
            await Task.Delay(processed > 0 ? BusyDelay : IdleDelay, stoppingToken);
        }
    }

    private async Task<int> DrainAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<IAppDbContext>();
        var channels = sp.GetServices<INotificationChannel>().ToDictionary(c => c.Channel);
        var clock = sp.GetRequiredService<IClock>();

        var queued = await db.Set<NotificationLog>()
            .Where(n => n.Status == NotificationStatus.Queued || n.Status == NotificationStatus.Sending)
            .OrderBy(n => n.OccurredAtUtc)
            .Take(BatchSize)
            .ToListAsync(stoppingToken);

        if (queued.Count == 0) return 0;

        foreach (var log in queued)
        {
            if (!channels.TryGetValue(log.Channel, out var channel))
            {
                log.Status = NotificationStatus.Failed;
                log.FailureReason = $"No channel registered for {log.Channel}.";
                continue;
            }

            log.Status = NotificationStatus.Sending;
            log.AttemptCount++;
            await db.SaveChangesAsync(stoppingToken);

            var envelope = new NotificationDispatchEnvelope(log.NotificationId, log.Recipient ?? "", log.Subject, log.Body ?? "");
            var result = await channel.SendAsync(envelope, stoppingToken);

            if (result.Success)
            {
                log.Status = NotificationStatus.Sent;
                log.ProviderId = result.ProviderId;
                log.DeliveredAtUtc = clock.UtcNow;
            }
            else if (log.AttemptCount >= MaxAttempts)
            {
                log.Status = NotificationStatus.Failed;
                log.FailureReason = result.FailureReason;
            }
            else
            {
                log.Status = NotificationStatus.Queued; // retry next pass
                log.FailureReason = result.FailureReason;
            }
            await db.SaveChangesAsync(stoppingToken);
        }
        return queued.Count;
    }
}
