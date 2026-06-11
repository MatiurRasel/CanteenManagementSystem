// =============================================================================
// DirectoryAdminController  (CanteenManagementSystem.Presentation.Controllers.Api.V1)
// -----------------------------------------------------------------------------
// Tenant-admin surface for managing the local directory + its sync source.
//
// REST surface
//   GET    /api/v1/admin/directory/config       current source config (secrets masked)
//   PUT    /api/v1/admin/directory/config       update source + credentials
//   POST   /api/v1/admin/directory/sync-now     trigger an immediate pull sync
//   POST   /api/v1/admin/directory/students     upload students CSV (multipart)
//   POST   /api/v1/admin/directory/employees    upload employees CSV (multipart)
//   GET    /api/v1/admin/directory/runs         last 50 sync runs (audit)
//   GET    /api/v1/admin/directory/health       summary: status / failures / last sync
//
// ADR 0004 — no IAppDbContext: roster/run reads go through
// IDirectoryAdminQueryService (Application/Infrastructure).
//
// SECRETS
//   PUT /config persists DB connection string + API key/secret with
//   isSecret=true so DataProtection encrypts them at rest. GET /config
//   returns "***" placeholders for those fields, never the cleartext.
// =============================================================================

using CanteenManagementSystem.Application.Directory;
using CanteenManagementSystem.Infrastructure.Directory.Csv;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Abstractions.Directory;
using Platform.Domain.Directory;

namespace CanteenManagementSystem.Presentation.Controllers.Api.V1;

[ApiController]
[Route("api/v1/admin/directory")]
[Authorize(Policy = "TenantAdmin")]
public sealed class DirectoryAdminController : ControllerBase
{
    private readonly IDirectorySyncService _sync;
    private readonly IDirectoryAdminQueryService _query;
    private readonly ITenantSettings _settings;

    private const string SecretPlaceholder = "***";

    public DirectoryAdminController(
        IDirectorySyncService sync,
        IDirectoryAdminQueryService query,
        ITenantSettings settings)
    {
        _sync     = sync;
        _query    = query;
        _settings = settings;
    }

    // ─── 1. Source config ─────────────────────────────────────────────────

    public sealed record DirectoryConfigDto(
        string Source,                // "Database" | "Api" | "Manual" | "Disabled"
        string? DatabaseConnectionString,
        string? DatabaseStudentsView,
        string? DatabaseEmployeesView,
        string? ApiBaseUrl,
        string? ApiKey,
        string? ApiSecret,
        int? ApiPageSize,
        int SyncIntervalMinutes,
        string SnapshotMode,
        string HealthStatus,
        int ConsecutiveFailures,
        int MaxFailuresBeforeAlert,
        string? AlertEmail,
        string? AlertPhone,
        DateTime? LastSyncAtUtc);

    [HttpGet("config")]
    public async Task<ActionResult<DirectoryConfigDto>> GetConfig(CancellationToken ct)
    {
        var raw = await ReadAllConfigAsync(ct);
        var lastSyncRaw = raw[DirectorySettingsKeys.LastSyncAtUtc];
        DateTime? lastSync = DateTime.TryParse(lastSyncRaw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : null;

        // Mask secrets — never return cleartext.
        return Ok(new DirectoryConfigDto(
            Source:                   raw[DirectorySettingsKeys.Source] ?? "Manual",
            DatabaseConnectionString: Mask(raw[DirectorySettingsKeys.DatabaseConnectionString]),
            DatabaseStudentsView:     raw[DirectorySettingsKeys.DatabaseStudentsView],
            DatabaseEmployeesView:    raw[DirectorySettingsKeys.DatabaseEmployeesView],
            ApiBaseUrl:               raw[DirectorySettingsKeys.ApiBaseUrl],
            ApiKey:                   Mask(raw[DirectorySettingsKeys.ApiKey]),
            ApiSecret:                Mask(raw[DirectorySettingsKeys.ApiSecret]),
            ApiPageSize:              TryInt(raw[DirectorySettingsKeys.ApiPageSize]),
            SyncIntervalMinutes:      TryInt(raw[DirectorySettingsKeys.SyncIntervalMinutes]) ?? 30,
            SnapshotMode:             raw[DirectorySettingsKeys.SnapshotMode] ?? "PerSync",
            HealthStatus:             raw[DirectorySettingsKeys.HealthStatus] ?? "Healthy",
            ConsecutiveFailures:      TryInt(raw[DirectorySettingsKeys.ConsecutiveFailures]) ?? 0,
            MaxFailuresBeforeAlert:   TryInt(raw[DirectorySettingsKeys.MaxFailuresBeforeAlert]) ?? 3,
            AlertEmail:               raw[DirectorySettingsKeys.AlertEmail],
            AlertPhone:               raw[DirectorySettingsKeys.AlertPhone],
            LastSyncAtUtc:            lastSync));
    }

    [HttpPut("config")]
    public async Task<ActionResult<DirectoryConfigDto>> UpdateConfig(
        [FromBody] DirectoryConfigDto patch, CancellationToken ct)
    {
        if (patch is null) return BadRequest(new { error = "Empty body." });

        if (!IsAllowedSource(patch.Source))
            return BadRequest(new { error = "Source must be Database | Api | Manual | Disabled." });

        await _settings.SetAsync(DirectorySettingsKeys.Source, patch.Source, cancellationToken: ct);

        // Only OVERWRITE secret fields when the caller actually supplied a
        // non-placeholder value — round-tripping the masked DTO must not wipe
        // the stored credential.
        if (patch.DatabaseConnectionString is not null && patch.DatabaseConnectionString != SecretPlaceholder)
            await _settings.SetAsync(DirectorySettingsKeys.DatabaseConnectionString, patch.DatabaseConnectionString, isSecret: true, cancellationToken: ct);
        if (patch.ApiKey is not null && patch.ApiKey != SecretPlaceholder)
            await _settings.SetAsync(DirectorySettingsKeys.ApiKey, patch.ApiKey, isSecret: true, cancellationToken: ct);
        if (patch.ApiSecret is not null && patch.ApiSecret != SecretPlaceholder)
            await _settings.SetAsync(DirectorySettingsKeys.ApiSecret, patch.ApiSecret, isSecret: true, cancellationToken: ct);

        // Non-secret fields: a NULL means "leave as is"; an empty string clears.
        await SetIfProvidedAsync(DirectorySettingsKeys.DatabaseStudentsView,  patch.DatabaseStudentsView,  ct);
        await SetIfProvidedAsync(DirectorySettingsKeys.DatabaseEmployeesView, patch.DatabaseEmployeesView, ct);
        await SetIfProvidedAsync(DirectorySettingsKeys.ApiBaseUrl,            patch.ApiBaseUrl,            ct);
        await SetIfProvidedAsync(DirectorySettingsKeys.ApiPageSize,           patch.ApiPageSize?.ToString(), ct);
        await SetIfProvidedAsync(DirectorySettingsKeys.SyncIntervalMinutes,   patch.SyncIntervalMinutes.ToString(), ct);
        await SetIfProvidedAsync(DirectorySettingsKeys.SnapshotMode,          patch.SnapshotMode, ct);
        await SetIfProvidedAsync(DirectorySettingsKeys.MaxFailuresBeforeAlert,patch.MaxFailuresBeforeAlert.ToString(), ct);
        await SetIfProvidedAsync(DirectorySettingsKeys.AlertEmail,            patch.AlertEmail, ct);
        await SetIfProvidedAsync(DirectorySettingsKeys.AlertPhone,            patch.AlertPhone, ct);

        return await GetConfig(ct);
    }

    // ─── 2. Sync triggers ─────────────────────────────────────────────────

    /// <summary>Run a sync against the currently-configured source immediately. Returns the run summary.</summary>
    [HttpPost("sync-now")]
    public async Task<ActionResult<DirectorySyncRunSummary>> SyncNow(CancellationToken ct)
    {
        var summary = await _sync.SyncCurrentTenantAsync(ct);
        if (summary is null)
            return BadRequest(new { error = "No directory source is configured for this tenant. Set Directory.Source to Database / Api first, or upload a CSV." });
        return Ok(summary);
    }

    // ─── 3. CSV uploads ───────────────────────────────────────────────────

    [HttpPost("students")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<DirectorySyncRunSummary>> UploadStudents(
        [FromForm] IFormFile file,
        [FromQuery(Name = "snapshot")] bool isFullSnapshot = true,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "Empty file." });

        IReadOnlyList<DirectoryStudent> students;
        try
        {
            await using var stream = file.OpenReadStream();
            students = DirectoryCsvParser.ParseStudents(stream);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }

        var delta = new DirectoryDelta(
            Students: students, Employees: Array.Empty<DirectoryEmployee>(),
            HighWatermarkUtc: DateTime.UtcNow, IsFullSnapshot: isFullSnapshot);
        return Ok(await _sync.IngestAsync(delta, sourceLabel: "Manual.Csv.Students", ct));
    }

    [HttpPost("employees")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<DirectorySyncRunSummary>> UploadEmployees(
        [FromForm] IFormFile file,
        [FromQuery(Name = "snapshot")] bool isFullSnapshot = true,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0) return BadRequest(new { error = "Empty file." });

        IReadOnlyList<DirectoryEmployee> employees;
        try
        {
            await using var stream = file.OpenReadStream();
            employees = DirectoryCsvParser.ParseEmployees(stream);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }

        var delta = new DirectoryDelta(
            Students: Array.Empty<DirectoryStudent>(), Employees: employees,
            HighWatermarkUtc: DateTime.UtcNow, IsFullSnapshot: isFullSnapshot);
        return Ok(await _sync.IngestAsync(delta, sourceLabel: "Manual.Csv.Employees", ct));
    }

    // ─── 4. Audit + health views ──────────────────────────────────────────

    [HttpGet("runs")]
    public async Task<ActionResult<IReadOnlyList<DirectorySyncRunSummary>>> GetRuns(
        [FromQuery] int take = 50, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        return Ok(await _query.GetRecentRunSummariesAsync(take, ct));
    }

    public sealed record DirectoryHealthDto(
        string Status,
        int ConsecutiveFailures,
        DateTime? LastSyncAtUtc,
        DateTime? LastSuccessAtUtc,
        string? LastErrorMessage,
        int StudentsTotal,
        int EmployeesTotal);

    [HttpGet("health")]
    public async Task<ActionResult<DirectoryHealthDto>> GetHealth(CancellationToken ct)
    {
        var status   = (await _settings.GetAsync(DirectorySettingsKeys.HealthStatus, "Healthy", ct)) ?? "Healthy";
        var failures = await _settings.GetIntAsync(DirectorySettingsKeys.ConsecutiveFailures, 0, ct);
        var lastSync = await _settings.GetAsync(DirectorySettingsKeys.LastSyncAtUtc, defaultValue: null, ct);
        DateTime? lastSyncAt = DateTime.TryParse(lastSync, null, System.Globalization.DateTimeStyles.RoundtripKind, out var ls) ? ls : null;

        var snapshot = await _query.GetHealthSnapshotAsync(ct);

        return Ok(new DirectoryHealthDto(
            Status: status, ConsecutiveFailures: failures,
            LastSyncAtUtc: lastSyncAt, LastSuccessAtUtc: snapshot.LastSuccessAtUtc,
            LastErrorMessage: snapshot.LastErrorMessage,
            StudentsTotal: snapshot.StudentsTotal, EmployeesTotal: snapshot.EmployeesTotal));
    }

    // ─── helpers ──────────────────────────────────────────────────────────

    private async Task SetIfProvidedAsync(string key, string? value, CancellationToken ct)
    {
        if (value is null) return;     // null -> leave existing alone
        await _settings.SetAsync(key, value, cancellationToken: ct);
    }

    private async Task<IReadOnlyDictionary<string, string?>> ReadAllConfigAsync(CancellationToken ct)
    {
        var keys = new[]
        {
            DirectorySettingsKeys.Source,
            DirectorySettingsKeys.DatabaseConnectionString,
            DirectorySettingsKeys.DatabaseStudentsView,
            DirectorySettingsKeys.DatabaseEmployeesView,
            DirectorySettingsKeys.ApiBaseUrl,
            DirectorySettingsKeys.ApiKey,
            DirectorySettingsKeys.ApiSecret,
            DirectorySettingsKeys.ApiPageSize,
            DirectorySettingsKeys.SyncIntervalMinutes,
            DirectorySettingsKeys.SnapshotMode,
            DirectorySettingsKeys.HealthStatus,
            DirectorySettingsKeys.ConsecutiveFailures,
            DirectorySettingsKeys.MaxFailuresBeforeAlert,
            DirectorySettingsKeys.AlertEmail,
            DirectorySettingsKeys.AlertPhone,
            DirectorySettingsKeys.LastSyncAtUtc
        };
        var dict = new Dictionary<string, string?>(keys.Length);
        foreach (var k in keys) dict[k] = await _settings.GetAsync(k, defaultValue: null, ct);
        return dict;
    }

    private static bool IsAllowedSource(string s)
        => s is "Database" or "Api" or "Manual" or "Disabled";

    private static string? Mask(string? s) => string.IsNullOrEmpty(s) ? null : SecretPlaceholder;
    private static int? TryInt(string? s) => int.TryParse(s, out var n) ? n : null;
}
