// =============================================================================
// LowStockAlertService  (CanteenManagementSystem.Infrastructure.BackgroundJobs)
// -----------------------------------------------------------------------------
// Scans today's DailyMenu rows on each tick. When AvailableQuantity drops at or
// below the configured threshold the service:
//   1. Notifies the tenant admin via INotificationService (Sms or Email per
//      Inventory.LowStockChannel setting; recipient = Inventory.LowStockRecipient).
//   2. Stamps Inventory.LowStock.{dailyMenuId} = "alerted-yyyy-MM-dd" so we
//      don't spam — one alert per item per day.
//
// THRESHOLD RESOLUTION
//   1. FoodItem.LowStockThreshold  (per-item override, NULL = use tenant default)
//   2. TenantSetting "Inventory.LowStockThreshold" (default 5)
//
// CROSS-TENANT EXECUTION (same pattern as AuditRetentionService)
//   Enumerates active tenants → per-tenant DI scope with stamped ITenantContext →
//   read today's menu → compare → notify. One bad tenant cannot stop the others.
// =============================================================================

using CanteenManagementSystem.Domain.Menu;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Abstractions.Notifications;
using Platform.Application.Abstractions.Tenancy;
using Platform.Application.Persistence;
using Platform.Domain.Notifications;
using Platform.Domain.Tenancy;

namespace CanteenManagementSystem.Infrastructure.BackgroundJobs;

public sealed class LowStockAlertService : BackgroundService
{
    public static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan ErrorBackoff = TimeSpan.FromMinutes(15);
    public const int DefaultThreshold = 5;

    private readonly IServiceProvider _sp;
    private readonly ILogger<LowStockAlertService> _logger;

    public LowStockAlertService(IServiceProvider sp, ILogger<LowStockAlertService> logger)
    {
        _sp = sp; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Low-stock alert service started; ticking every {Tick}.", TickInterval);
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAllTenantsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Low-stock sweep failed; backing off {Backoff}.", ErrorBackoff);
                try { await Task.Delay(ErrorBackoff, stoppingToken); }
                catch (OperationCanceledException) { break; }
                continue;
            }

            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ScanAllTenantsAsync(CancellationToken ct)
    {
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
            await ScanOneTenantAsync(t.ClientId, t.ClientCode, ct);
        }
    }

    private async Task ScanOneTenantAsync(int clientId, string clientCode, CancellationToken ct)
    {
        try
        {
            await using var scope = _sp.CreateAsyncScope();
            var sp = scope.ServiceProvider;
            if (sp.GetRequiredService<Platform.Domain.Tenancy.ITenantContext>() is IMutableTenantContext mutable)
            {
                mutable.Resolve(clientId, clientCode);
            }

            var settings = sp.GetRequiredService<ITenantSettings>();
            var enabled = await settings.GetBoolAsync("Inventory.LowStockEnabled", true, ct);
            if (!enabled) return;

            var defaultThreshold = await settings.GetIntAsync("Inventory.LowStockThreshold", DefaultThreshold, ct);
            var recipient = await settings.GetAsync("Inventory.LowStockRecipient", defaultValue: null, ct);
            if (string.IsNullOrWhiteSpace(recipient)) return;     // No-one to notify
            var channelText = await settings.GetAsync("Inventory.LowStockChannel", "Email", ct);
            var channel = Enum.TryParse<NotificationChannel>(channelText, ignoreCase: true, out var c) ? c : NotificationChannel.Email;

            var menus = sp.GetRequiredService<IReadOnlyRepository<DailyMenu>>();
            var today = DateTime.Today;
            var lowItems = await menus.NoTrackingQuery()
                .Include(dm => dm.FoodItem)
                .Where(dm => dm.MenuDate.Date == today && dm.IsAvailable)
                .Select(dm => new
                {
                    dm.DailyMenuID,
                    ItemName = dm.FoodItem.ItemName,
                    dm.AvailableQuantity,
                    Threshold = dm.FoodItem.LowStockThreshold ?? defaultThreshold
                })
                .ToListAsync(ct);

            if (lowItems.Count == 0) return;

            var notifications = sp.GetRequiredService<INotificationService>();
            var todayKey = today.ToString("yyyy-MM-dd");
            var alerted = 0;

            foreach (var item in lowItems.Where(i => i.AvailableQuantity <= i.Threshold))
            {
                var settingKey = $"Inventory.LowStock.Alerted.{item.DailyMenuID}";
                var lastAlerted = await settings.GetAsync(settingKey, defaultValue: null, ct);
                if (string.Equals(lastAlerted, todayKey, StringComparison.Ordinal)) continue;

                var subject = $"Low stock: {item.ItemName}";
                var body = $"{item.ItemName} has {item.AvailableQuantity} left (threshold {item.Threshold}). " +
                           "Restock or pull from the menu before the lunch rush.";
                await notifications.SendRawAsync(channel, recipient!, subject, body, ct);
                await settings.SetAsync(settingKey, todayKey, cancellationToken: ct);
                alerted++;
            }

            if (alerted > 0)
            {
                _logger.LogInformation("Low-stock alerts sent: {Count} item(s) for tenant {Tenant}.", alerted, clientCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Low-stock scan failed for tenant {Tenant}.", clientCode);
        }
    }
}
