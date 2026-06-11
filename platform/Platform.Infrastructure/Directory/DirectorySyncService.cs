// =============================================================================
// DirectorySyncService  (Platform.Infrastructure.Directory)
// -----------------------------------------------------------------------------
// Generic orchestrator. Three entry points, one engine:
//   IngestAsync             — caller already has a delta (CSV / webhook)
//   SyncCurrentTenantAsync  — resolve source via factory, pull, write
//   SyncAllDueAsync         — enumerate tenants, scope-per-tenant, dispatch
//
// AUDIT
//   Every invocation produces ONE DirectorySyncRun row. The row is inserted
//   with Status="Running" BEFORE the writer is called, so the admin UI can
//   show in-flight runs. On finish: Status="Success" / "Failed" + counts +
//   ErrorMessage + CompletedAtUtc.
//
// CIRCUIT BREAKER  (Phase 6)
//   Reads Directory.ConsecutiveFailures + Directory.HealthStatus from tenant
//   settings. On failure: ConsecutiveFailures++; if it crosses
//   MaxFailuresBeforeAlert, HealthStatus -> "Failing" AND a notification is
//   fired via INotificationService (best-effort). On success: counter and
//   status reset to 0 / "Healthy".
//
// ADR 0004: IUnitOfWork + IReadOnlyRepository<Client> + IRepository<DirectorySyncRun>.
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Abstractions.Directory;
using Platform.Application.Abstractions.Notifications;
using Platform.Application.Abstractions.RealTime;
using Platform.Application.Abstractions.Tenancy;
using Platform.Application.Persistence;
using Platform.Domain.Directory;
using Platform.Domain.Notifications;
using Platform.Domain.Tenancy;

namespace Platform.Infrastructure.Directory;

public sealed class DirectorySyncService : IDirectorySyncService
{
    private readonly IServiceProvider _sp;
    private readonly IUnitOfWork _uow;
    private readonly IReadOnlyRepository<Client> _clients;
    private readonly IDirectoryWriter _writer;
    private readonly IDirectorySourceFactory _sourceFactory;
    private readonly ITenantSettings _settings;
    private readonly INotificationService _notifications;
    private readonly IOrderBroadcaster? _broadcaster;
    private readonly ILogger<DirectorySyncService> _logger;

    public DirectorySyncService(
        IServiceProvider sp,
        IUnitOfWork uow,
        IReadOnlyRepository<Client> clients,
        IDirectoryWriter writer,
        IDirectorySourceFactory sourceFactory,
        ITenantSettings settings,
        INotificationService notifications,
        ILogger<DirectorySyncService> logger,
        IOrderBroadcaster? broadcaster = null)
    {
        _sp            = sp;
        _uow           = uow;
        _clients       = clients;
        _writer        = writer;
        _sourceFactory = sourceFactory;
        _settings      = settings;
        _notifications = notifications;
        _broadcaster   = broadcaster;
        _logger        = logger;
    }

    private IRepository<DirectorySyncRun> Runs => _uow.Repository<DirectorySyncRun>();

    // ─── 1. CSV / webhook ingest (pre-built delta) ────────────────────────

    public Task<DirectorySyncRunSummary> IngestAsync(
        DirectoryDelta delta,
        string sourceLabel,
        CancellationToken cancellationToken = default)
        => RunOneAsync(sourceLabel, _ => Task.FromResult(delta), cancellationToken)!;

    // ─── 2. Pull from current tenant's configured source ──────────────────

    public async Task<DirectorySyncRunSummary?> SyncCurrentTenantAsync(CancellationToken cancellationToken = default)
    {
        var source = await _sourceFactory.ResolveCurrentTenantAsync(cancellationToken);
        if (source is null)
        {
            _logger.LogDebug("No directory source configured for current tenant; skipping.");
            return null;
        }

        // Source column is nvarchar(20). Strip the "DirectorySource" suffix so
        // "Pull.ManualDirectorySource" → "Pull.Manual" stays under the cap.
        var typeName = source.GetType().Name
            .Replace("DirectorySource", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Source", string.Empty, StringComparison.OrdinalIgnoreCase);
        var label = $"Pull.{typeName}";
        if (label.Length > 20) label = label[..20];
        return await RunOneAsync(label,
            async ct =>
            {
                var since = await ReadLastSyncAtAsync(ct);
                return await source.FetchAsync(since, ct);
            },
            cancellationToken);
    }

    // ─── 3. Background loop: per active tenant, scoped, due-only ─────────

    public async Task<IReadOnlyList<DirectorySyncRunSummary>> SyncAllDueAsync(CancellationToken cancellationToken = default)
    {
        // Enumerate tenants with the query filter off (we ARE crossing tenants here).
        var tenants = await _clients.NoTrackingQuery()
            .IgnoreQueryFilters()
            .Where(c => c.IsActive)
            .ToListAsync(cancellationToken);

        var results = new List<DirectorySyncRunSummary>();
        foreach (var tenant in tenants)
        {
            if (cancellationToken.IsCancellationRequested) break;

            await using var scope = _sp.CreateAsyncScope();
            // Stamp the tenant context inside the new scope so all downstream
            // services (settings, source factory, writer queries) see ClientId.
            if (scope.ServiceProvider.GetRequiredService<ITenantContext>() is IMutableTenantContext mutable)
            {
                mutable.Resolve(tenant.ClientId, tenant.ClientCode);
            }

            var settings  = scope.ServiceProvider.GetRequiredService<ITenantSettings>();
            var status    = await settings.GetAsync(DirectorySettingsKeys.HealthStatus, "Healthy", cancellationToken);
            if (string.Equals(status, "Disabled", StringComparison.OrdinalIgnoreCase))
            {
                continue;   // tenant admin manually paused; honour that
            }

            var lastSync   = await ReadLastSyncAtAsync(settings, cancellationToken);
            var intervalMin = await settings.GetIntAsync(DirectorySettingsKeys.SyncIntervalMinutes, 30, cancellationToken);
            if (DateTime.UtcNow - lastSync < TimeSpan.FromMinutes(Math.Max(1, intervalMin)))
            {
                continue;   // not due yet
            }

            try
            {
                var nestedSvc = scope.ServiceProvider.GetRequiredService<IDirectorySyncService>();
                var summary   = await nestedSvc.SyncCurrentTenantAsync(cancellationToken);
                if (summary is not null) results.Add(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tenant {ClientCode} directory sync errored at background tick.", tenant.ClientCode);
            }
        }
        return results;
    }

    // ─── core: write the audit row, dispatch, update on result ────────────

    private async Task<DirectorySyncRunSummary> RunOneAsync(
        string sourceLabel,
        Func<CancellationToken, Task<DirectoryDelta>> produceDelta,
        CancellationToken cancellationToken)
    {
        var run = new DirectorySyncRun
        {
            StartedAtUtc = DateTime.UtcNow,
            Status       = "Running",
            Source       = sourceLabel
        };
        await Runs.AddAsync(run, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        try
        {
            var delta  = await produceDelta(cancellationToken);
            var result = await _writer.WriteAsync(delta, cancellationToken);

            run.StudentsAdded     = result.StudentsAdded;
            run.StudentsUpdated   = result.StudentsUpdated;
            run.StudentsDisabled  = result.StudentsDisabled;
            run.EmployeesAdded    = result.EmployeesAdded;
            run.EmployeesUpdated  = result.EmployeesUpdated;
            run.EmployeesDisabled = result.EmployeesDisabled;
            run.HighWatermarkUtc  = delta.HighWatermarkUtc;
            run.CompletedAtUtc    = DateTime.UtcNow;
            run.Status            = "Success";
            await _uow.SaveChangesAsync(cancellationToken);

            await OnSuccessAsync(delta.HighWatermarkUtc, cancellationToken);
            _logger.LogInformation(
                "Directory sync {Run} ({Source}) ok: students +{SA}/~{SU}/-{SD}, employees +{EA}/~{EU}/-{ED}",
                run.SyncRunId, run.Source,
                run.StudentsAdded, run.StudentsUpdated, run.StudentsDisabled,
                run.EmployeesAdded, run.EmployeesUpdated, run.EmployeesDisabled);

            await TryBroadcastAsync(run, cancellationToken);
        }
        catch (Exception ex)
        {
            run.CompletedAtUtc = DateTime.UtcNow;
            run.Status         = "Failed";
            run.ErrorMessage   = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            await _uow.SaveChangesAsync(CancellationToken.None);   // best-effort

            await OnFailureAsync(run, cancellationToken);
            _logger.LogError(ex, "Directory sync {Run} ({Source}) failed.", run.SyncRunId, run.Source);
            await TryBroadcastAsync(run, CancellationToken.None);
            // Don't re-throw from the background path — the orchestrator's job
            // is to leave the system in a healthy state regardless of source
            // brokenness. IngestAsync callers (CSV controller) read the
            // returned summary's Status to detect failure.
        }

        return ToSummary(run);
    }

    private async Task TryBroadcastAsync(DirectorySyncRun run, CancellationToken ct)
    {
        if (_broadcaster is null) return;
        try
        {
            await _broadcaster.DirectorySyncCompletedAsync(new
            {
                runId             = run.SyncRunId,
                status            = run.Status,
                source            = run.Source,
                studentsAdded     = run.StudentsAdded,
                studentsUpdated   = run.StudentsUpdated,
                studentsDisabled  = run.StudentsDisabled,
                employeesAdded    = run.EmployeesAdded,
                employeesUpdated  = run.EmployeesUpdated,
                employeesDisabled = run.EmployeesDisabled,
                durationSec       = run.CompletedAtUtc.HasValue
                    ? (int)Math.Round((run.CompletedAtUtc.Value - run.StartedAtUtc).TotalSeconds)
                    : 0,
                errorMessage      = run.ErrorMessage,
                completedAtUtc    = (run.CompletedAtUtc ?? DateTime.UtcNow).ToString("o")
            }, ct);
        }
        catch (Exception ex)
        {
            // Broadcast failures must never break the sync.
            _logger.LogWarning(ex, "Directory sync broadcast failed for run {Run}", run.SyncRunId);
        }
    }

    // ─── circuit breaker + alerts (Phase 6) ───────────────────────────────

    private async Task OnSuccessAsync(DateTime highWatermarkUtc, CancellationToken ct)
    {
        await _settings.SetAsync(DirectorySettingsKeys.LastSyncAtUtc,       highWatermarkUtc.ToString("O"), cancellationToken: ct);
        await _settings.SetAsync(DirectorySettingsKeys.ConsecutiveFailures, "0",                              cancellationToken: ct);
        await _settings.SetAsync(DirectorySettingsKeys.HealthStatus,        "Healthy",                        cancellationToken: ct);
    }

    private async Task OnFailureAsync(DirectorySyncRun run, CancellationToken ct)
    {
        var failures = await _settings.GetIntAsync(DirectorySettingsKeys.ConsecutiveFailures, 0, ct);
        failures++;
        var threshold = await _settings.GetIntAsync(DirectorySettingsKeys.MaxFailuresBeforeAlert, 3, ct);
        await _settings.SetAsync(DirectorySettingsKeys.ConsecutiveFailures, failures.ToString(), cancellationToken: ct);

        if (failures >= threshold)
        {
            await _settings.SetAsync(DirectorySettingsKeys.HealthStatus, "Failing", cancellationToken: ct);

            // Alert on the FIRST crossing only — don't spam after.
            if (failures == threshold)
            {
                await TryAlertAsync(run, failures, ct);
            }
        }
    }

    private async Task TryAlertAsync(DirectorySyncRun run, int failures, CancellationToken ct)
    {
        try
        {
            var email = await _settings.GetAsync(DirectorySettingsKeys.AlertEmail, defaultValue: null, ct);
            var phone = await _settings.GetAsync(DirectorySettingsKeys.AlertPhone, defaultValue: null, ct);
            var summary = $"Directory sync has failed {failures} consecutive times. Last error: {Truncate(run.ErrorMessage, 240)}";

            if (!string.IsNullOrWhiteSpace(email))
            {
                await _notifications.SendRawAsync(NotificationChannel.Email, email!,
                    subject: "Canteen — directory sync is failing",
                    body:    summary,
                    cancellationToken: ct);
            }
            if (!string.IsNullOrWhiteSpace(phone))
            {
                await _notifications.SendRawAsync(NotificationChannel.Sms, phone!,
                    subject: null, body: summary, cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send directory failure alert.");
        }
    }

    private async Task<DateTime> ReadLastSyncAtAsync(CancellationToken ct)
        => await ReadLastSyncAtAsync(_settings, ct);

    private static async Task<DateTime> ReadLastSyncAtAsync(ITenantSettings settings, CancellationToken ct)
    {
        var raw = await settings.GetAsync(DirectorySettingsKeys.LastSyncAtUtc, defaultValue: null, ct);
        return DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
            ? dt
            : DateTime.MinValue;
    }

    private static string Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? string.Empty : (s!.Length <= max ? s : s[..max] + "…");

    private static DirectorySyncRunSummary ToSummary(DirectorySyncRun run) => new(
        SyncRunId:        run.SyncRunId,
        Status:           run.Status,
        Source:           run.Source,
        StartedAtUtc:     run.StartedAtUtc,
        CompletedAtUtc:   run.CompletedAtUtc,
        StudentsAdded:    run.StudentsAdded,
        StudentsUpdated:  run.StudentsUpdated,
        StudentsDisabled: run.StudentsDisabled,
        EmployeesAdded:   run.EmployeesAdded,
        EmployeesUpdated: run.EmployeesUpdated,
        EmployeesDisabled:run.EmployeesDisabled,
        ErrorMessage:     run.ErrorMessage);
}
