// =============================================================================
// ReportSchedulerBackgroundService  (Platform.Infrastructure.Reporting)
// -----------------------------------------------------------------------------
// Hosted service. Wakes every minute, walks active tenants + their schedules,
// runs anything past its NextRunAtUtc, emails the rendered output via
// IReportDistributor, then re-stamps Next/Last.
//
// PER-TENANT SCOPE — each tenant gets its own DI scope so ITenantContext +
// ITenantSettings + IAppDbContext are resolved correctly (the EF query filter
// scopes everything to that tenant transparently).
//
// FAILURE ISOLATION — a broken schedule for one tenant cannot stop another;
// exceptions are caught at the schedule level + recorded on the row.
//
// SAFETY — if Migration:ApplyOnStartup is false AND the ReportSchedules table
// doesn't yet exist, the first DB read fails with a known SQL error and we
// log + back off. Once the migration is applied the loop self-heals.
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.Application.Abstractions.Reporting;
using Platform.Application.Abstractions.Tenancy;
using Platform.Application.Persistence;
using Platform.Domain.Reporting;
using Platform.Domain.Tenancy;

namespace Platform.Infrastructure.Reporting;

public sealed class ReportSchedulerBackgroundService : BackgroundService
{
    public static readonly TimeSpan TickInterval  = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan ErrorBackoff  = TimeSpan.FromMinutes(5);

    private readonly IServiceProvider _sp;
    private readonly ILogger<ReportSchedulerBackgroundService> _logger;

    public ReportSchedulerBackgroundService(IServiceProvider sp, ILogger<ReportSchedulerBackgroundService> logger)
    {
        _sp = sp; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Report scheduler started; ticking every {Tick}.", TickInterval);
        try { await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _sp.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

                // Cross-tenant enumeration (query filter is OFF).
                var tenants = await db.Set<Client>().IgnoreQueryFilters().AsNoTracking()
                    .Where(c => c.IsActive)
                    .Select(c => new { c.ClientId, c.ClientCode })
                    .ToListAsync(stoppingToken);

                foreach (var t in tenants)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    await ProcessTenantAsync(t.ClientId, t.ClientCode, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Report scheduler tick failed at the loop level; backing off {Backoff}.", ErrorBackoff);
                try { await Task.Delay(ErrorBackoff, stoppingToken); }
                catch (OperationCanceledException) { break; }
                continue;
            }

            try { await Task.Delay(TickInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("Report scheduler stopped.");
    }

    private async Task ProcessTenantAsync(int clientId, string clientCode, CancellationToken ct)
    {
        await using var tenantScope = _sp.CreateAsyncScope();
        var sp = tenantScope.ServiceProvider;
        if (sp.GetRequiredService<Platform.Domain.Tenancy.ITenantContext>() is IMutableTenantContext mut)
            mut.Resolve(clientId, clientCode);

        var db = sp.GetRequiredService<IAppDbContext>();

        List<ReportSchedule> due;
        try
        {
            due = await db.Set<ReportSchedule>()
                .Where(s => s.IsEnabled && s.NextRunAtUtc != null && s.NextRunAtUtc <= DateTime.UtcNow)
                .ToListAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Tenant {Tenant} schedule read failed (migration not applied?).", clientCode);
            return;
        }

        if (due.Count == 0) return;

        var dispatcher  = sp.GetRequiredService<IReportDispatcher>();
        var distributor = sp.GetRequiredService<IReportDistributor>();

        foreach (var schedule in due)
        {
            await RunOneAsync(schedule, dispatcher, distributor, db, ct);
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task RunOneAsync(
        ReportSchedule schedule, IReportDispatcher dispatcher, IReportDistributor distributor,
        IAppDbContext db, CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;
        try
        {
            // Resolve magic tokens (@TODAY etc.) then parse JSON → string-keyed dict.
            var expanded  = ReportParameterTokens.Expand(schedule.ParametersJson, nowUtc);
            var formData  = ParseToFormData(expanded);

            if (!Enum.TryParse<ReportFormat>(schedule.Format, ignoreCase: true, out var format))
                format = ReportFormat.Pdf;

            var rendered = await dispatcher.RunAsync(schedule.ReportKey, formData, format, ct);
            var recipients = (schedule.Recipients ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var subject = $"[Canteen] {schedule.DisplayName} — {DateTime.UtcNow:yyyy-MM-dd}";
            var body    = $@"<p>Hi,</p>
                             <p>Your scheduled report <b>{schedule.DisplayName}</b> is attached.</p>
                             <p style='color:#6b7280'>Generated automatically by the canteen platform at {DateTime.UtcNow:u}.</p>";

            var dist = await distributor.EmailAsync(recipients, subject, body, rendered, ct);

            schedule.LastRunAtUtc = nowUtc;
            schedule.LastRunStatus = dist.Success ? "Success" : "Failed";
            schedule.LastError     = dist.Success ? null : dist.Detail;
        }
        catch (Exception ex)
        {
            schedule.LastRunAtUtc  = nowUtc;
            schedule.LastRunStatus = "Failed";
            schedule.LastError     = Truncate(ex.Message, 2000);
            _logger.LogError(ex, "Schedule {Id} ({Report}) failed to run.", schedule.ScheduleId, schedule.ReportKey);
        }
        finally
        {
            schedule.NextRunAtUtc = ReportScheduleOccurrence.ComputeNext(
                schedule.Recurrence,
                schedule.HourOfDay, schedule.Minute,
                schedule.DayOfWeek, schedule.DayOfMonth, schedule.IntervalHours,
                nowUtc);
        }
        await Task.CompletedTask;
        _ = db;     // suppress unused
    }

    private static IReadOnlyDictionary<string, string?> ParseToFormData(string json)
    {
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    dict[p.Name] = p.Value.ValueKind switch
                    {
                        System.Text.Json.JsonValueKind.String => p.Value.GetString(),
                        System.Text.Json.JsonValueKind.Null   => null,
                        _ => p.Value.GetRawText()
                    };
                }
            }
        }
        catch { /* Malformed JSON falls through with empty dict — dispatcher will use defaults. */ }
        return dict;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
