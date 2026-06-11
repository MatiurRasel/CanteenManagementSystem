using Platform.Application.Persistence;
using Platform.Application.Abstractions.Wallets;
using Platform.Application.Configuration;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanteenManagementSystem.Infrastructure.BackgroundJobs;

/// §6.4 of the source of truth: orders left in Ready state past the configured
/// timeout must be auto-cancelled. Releases the wallet block and reservation.
internal sealed class ReadyOrderAutoCancelService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<CanteenConfiguration> _options;
    private readonly ILogger<ReadyOrderAutoCancelService> _logger;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    public ReadyOrderAutoCancelService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<CanteenConfiguration> options,
        ILogger<ReadyOrderAutoCancelService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredOrdersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto-cancel sweep failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ProcessExpiredOrdersAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var wallet = scope.ServiceProvider.GetRequiredService<IWalletService>();

        var cutoff = DateTime.Now.AddMinutes(-_options.CurrentValue.ReadyOrderAutoCancelMinutes);
        var expired = await db.Set<Order>()
            .Where(o => o.Status == CanteenOrderStatus.Ready && o.OrderDate <= cutoff)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0) return;

        foreach (var order in expired)
        {
            order.Status = CanteenOrderStatus.Cancelled;
            await wallet.ReleaseAsync(order.UserId, order.UserType.ToString(), order.TotalAmount, order.OrderID, "Auto-cancelled after ready-window expiry", cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Auto-cancelled {Count} expired ready orders", expired.Count);
    }
}
