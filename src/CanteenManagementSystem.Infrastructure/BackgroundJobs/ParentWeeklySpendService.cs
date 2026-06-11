// =============================================================================
// ParentWeeklySpendService  (CanteenManagementSystem.Infrastructure.BackgroundJobs)
// -----------------------------------------------------------------------------
// Sends a weekly spend digest to each parent whose linked children placed
// orders in the last 7 days. The send goes through the regular notification
// pipeline (template = NotificationTemplates.ParentWeeklySpend), so the
// admin can re-word it per tenant via /admin/notification-templates.
//
// SCHEDULE
//   * Runs on a 1h tick (cheap, mostly no-ops).
//   * Fires only when local time matches the configured hour-of-week.
//   * `Notifications.ParentWeeklySpend.DayOfWeek` (0–6, default Sunday=0).
//   * `Notifications.ParentWeeklySpend.HourOfDay`  (0–23, default 9).
//   * `Notifications.ParentWeeklySpend.Enabled`    (default true).
//
// MULTI-TENANT
//   Per-tenant scope; uses ITenantSettings + the EF global query filter.
// =============================================================================

using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Orders;
using CanteenManagementSystem.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Abstractions.Notifications;
using Platform.Application.Abstractions.Tenancy;
using Platform.Application.Persistence;
using Platform.Domain.Identity;
using Platform.Domain.Notifications;
using Platform.Domain.Tenancy;

namespace CanteenManagementSystem.Infrastructure.BackgroundJobs;

public sealed class ParentWeeklySpendService : BackgroundService
{
    public static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(60);
    public static readonly TimeSpan ErrorBackoff = TimeSpan.FromMinutes(15);

    private readonly IServiceProvider _sp;
    private readonly ILogger<ParentWeeklySpendService> _logger;

    public ParentWeeklySpendService(IServiceProvider sp, ILogger<ParentWeeklySpendService> logger)
    {
        _sp = sp; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Parent weekly-spend service started; tick {Tick}.", TickInterval);
        try { await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Weekly-spend sweep failed; backing off {Backoff}.", ErrorBackoff);
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
            await DigestTenantAsync(t.ClientId, t.ClientCode, ct);
        }
    }

    private async Task DigestTenantAsync(int clientId, string clientCode, CancellationToken ct)
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
            var enabled  = await settings.GetBoolAsync("Notifications.ParentWeeklySpend.Enabled", true, ct);
            if (!enabled) return;
            var dayOfWeek = await settings.GetIntAsync("Notifications.ParentWeeklySpend.DayOfWeek", 0, ct);
            var hourOfDay = await settings.GetIntAsync("Notifications.ParentWeeklySpend.HourOfDay", 9, ct);
            var now = DateTime.Now;
            if ((int)now.DayOfWeek != dayOfWeek || now.Hour != hourOfDay) return;

            // Idempotency per-tenant: don't fire twice in the same hour-window.
            var thisWindow = now.ToString("yyyy-MM-dd-HH");
            var lastWindow = await settings.GetAsync("Notifications.ParentWeeklySpend.LastWindow", defaultValue: null, ct);
            if (string.Equals(thisWindow, lastWindow, StringComparison.Ordinal)) return;

            var notifications = sp.GetRequiredService<INotificationService>();
            var users     = sp.GetRequiredService<IReadOnlyRepository<User>>();
            var orders    = sp.GetRequiredService<IReadOnlyRepository<Order>>();
            var students  = sp.GetRequiredService<IReadOnlyRepository<Student>>();

            var weekStart = now.AddDays(-7);
            var parents = await users.NoTrackingQuery()
                .Where(u => u.IsActive && u.LinkedChildrenCsv != null && u.LinkedChildrenCsv != "")
                .Select(u => new { u.UserId, u.UserName, u.Email, u.LinkedChildrenCsv })
                .ToListAsync(ct);

            var sent = 0;
            foreach (var parent in parents)
            {
                if (string.IsNullOrWhiteSpace(parent.Email)) continue;
                var childIds = parent.LinkedChildrenCsv!
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
                if (childIds.Count == 0) continue;

                foreach (var childId in childIds)
                {
                    var weekly = await orders.NoTrackingQuery()
                        .Where(o => o.UserId == childId
                                 && o.UserType == CanteenUserType.Student
                                 && o.OrderDate >= weekStart
                                 && o.Status == CanteenOrderStatus.Delivered)
                        .Include(o => o.OrderItems).ThenInclude(oi => oi.FoodItem)
                        .ToListAsync(ct);
                    if (weekly.Count == 0) continue;

                    var amount = weekly.Sum(o => o.TotalAmount);
                    var ordersCount = weekly.Count;
                    var topItems = weekly
                        .SelectMany(o => o.OrderItems)
                        .GroupBy(oi => oi.FoodItem?.ItemName ?? "—")
                        .OrderByDescending(g => g.Sum(x => x.Quantity))
                        .Take(3)
                        .Select(g => g.Key);
                    var topItemsCsv = string.Join(", ", topItems);

                    var childRow = await students.NoTrackingQuery()
                        .Where(s => s.ExternalId == childId).Select(s => s.Name).FirstOrDefaultAsync(ct);
                    await notifications.SendAsync(
                        templateKey: NotificationTemplates.ParentWeeklySpend,
                        recipient: parent.Email,
                        tokens: new { child = childRow ?? childId, amount, orders = ordersCount, topItems = topItemsCsv },
                        overrideChannel: NotificationChannel.Email,
                        userId: parent.UserName,
                        cancellationToken: ct);
                    sent++;
                }
            }

            await settings.SetAsync("Notifications.ParentWeeklySpend.LastWindow", thisWindow, cancellationToken: ct);
            if (sent > 0)
            {
                _logger.LogInformation("Weekly-spend digest sent {Count} email(s) for tenant {Tenant}.", sent, clientCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Weekly-spend digest failed for tenant {Tenant}.", clientCode);
        }
    }
}
