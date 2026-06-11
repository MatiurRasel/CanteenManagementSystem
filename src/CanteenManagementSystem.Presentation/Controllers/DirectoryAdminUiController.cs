// =============================================================================
// DirectoryAdminUiController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Razor-view layer that pairs with the JSON API in
// Controllers/Api/V1/DirectoryAdminController. Reads route through
// IDirectoryAdminQueryService (ADR 0004 — no IAppDbContext).
//
// The views are designed to be operator-friendly:
//   /admin/directory             - landing: health badge + last 10 runs
//   /admin/directory/setup       - source config form (Database / API / Manual)
//   /admin/directory/runs        - paginated sync run history
//   /admin/directory/students    - paginated synced student roster
//   /admin/directory/employees   - paginated synced employee roster
// =============================================================================

using CanteenManagementSystem.Application.Directory;
using CanteenManagementSystem.Domain.Users;
using CanteenManagementSystem.Infrastructure.Directory.Csv;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Abstractions.Directory;
using Platform.Domain.Directory;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "TenantAdmin")]
[Route("admin/directory")]
public sealed class DirectoryAdminUiController : Controller
{
    private readonly IDirectoryAdminQueryService _query;
    private readonly ITenantSettings _settings;
    private readonly IDirectorySyncService _sync;

    public DirectoryAdminUiController(
        IDirectoryAdminQueryService query,
        ITenantSettings settings,
        IDirectorySyncService sync)
    {
        _query = query; _settings = settings; _sync = sync;
    }

    // ─── 1. Landing — Directory Health ────────────────────────────────────

    public sealed record HealthVm(
        string Status, int ConsecutiveFailures, DateTime? LastSyncAtUtc,
        DateTime? LastSuccessAtUtc, string? LastErrorMessage,
        int StudentsTotal, int EmployeesTotal, string Source,
        IReadOnlyList<DirectorySyncRun> RecentRuns);

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var status   = (await _settings.GetAsync(DirectorySettingsKeys.HealthStatus, "Healthy", ct)) ?? "Healthy";
        var source   = (await _settings.GetAsync(DirectorySettingsKeys.Source,       "Manual",  ct)) ?? "Manual";
        var failures = await _settings.GetIntAsync(DirectorySettingsKeys.ConsecutiveFailures, 0, ct);
        var lastSync = await _settings.GetAsync(DirectorySettingsKeys.LastSyncAtUtc, defaultValue: null, ct);
        DateTime? lastSyncAt = DateTime.TryParse(lastSync, null, System.Globalization.DateTimeStyles.RoundtripKind, out var ls) ? ls : null;

        var snapshot = await _query.GetHealthSnapshotAsync(ct);
        var recentRuns = await _query.GetRecentRunsAsync(10, ct);

        return View(new HealthVm(
            Status: status, ConsecutiveFailures: failures, LastSyncAtUtc: lastSyncAt,
            LastSuccessAtUtc: snapshot.LastSuccessAtUtc, LastErrorMessage: snapshot.LastErrorMessage,
            StudentsTotal: snapshot.StudentsTotal, EmployeesTotal: snapshot.EmployeesTotal,
            Source: source, RecentRuns: recentRuns));
    }

    // ─── 2. Setup — source config ─────────────────────────────────────────

    public sealed class SetupForm
    {
        public string Source { get; set; } = "Manual";
        public string? DatabaseConnectionString { get; set; }
        public string? DatabaseStudentsView { get; set; }
        public string? DatabaseEmployeesView { get; set; }
        public string? ApiBaseUrl { get; set; }
        public string? ApiKey { get; set; }
        public string? ApiSecret { get; set; }
        public int ApiPageSize { get; set; } = 500;
        public int SyncIntervalMinutes { get; set; } = 30;
        public string SnapshotMode { get; set; } = "PerSync";
        public int MaxFailuresBeforeAlert { get; set; } = 3;
        public string? AlertEmail { get; set; }
        public string? AlertPhone { get; set; }

        public bool HasDbConnectionStored { get; set; }
        public bool HasApiKeyStored { get; set; }
        public bool HasApiSecretStored { get; set; }
    }

    [HttpGet("setup")]
    public async Task<IActionResult> Setup(CancellationToken ct)
    {
        var form = new SetupForm
        {
            Source                   = (await _settings.GetAsync(DirectorySettingsKeys.Source, "Manual", ct)) ?? "Manual",
            DatabaseStudentsView     = await _settings.GetAsync(DirectorySettingsKeys.DatabaseStudentsView, defaultValue: null, ct),
            DatabaseEmployeesView    = await _settings.GetAsync(DirectorySettingsKeys.DatabaseEmployeesView, defaultValue: null, ct),
            ApiBaseUrl               = await _settings.GetAsync(DirectorySettingsKeys.ApiBaseUrl, defaultValue: null, ct),
            ApiPageSize              = await _settings.GetIntAsync(DirectorySettingsKeys.ApiPageSize, 500, ct),
            SyncIntervalMinutes      = await _settings.GetIntAsync(DirectorySettingsKeys.SyncIntervalMinutes, 30, ct),
            SnapshotMode             = (await _settings.GetAsync(DirectorySettingsKeys.SnapshotMode, "PerSync", ct)) ?? "PerSync",
            MaxFailuresBeforeAlert   = await _settings.GetIntAsync(DirectorySettingsKeys.MaxFailuresBeforeAlert, 3, ct),
            AlertEmail               = await _settings.GetAsync(DirectorySettingsKeys.AlertEmail, defaultValue: null, ct),
            AlertPhone               = await _settings.GetAsync(DirectorySettingsKeys.AlertPhone, defaultValue: null, ct),
            HasDbConnectionStored    = !string.IsNullOrWhiteSpace(await _settings.GetAsync(DirectorySettingsKeys.DatabaseConnectionString, defaultValue: null, ct)),
            HasApiKeyStored          = !string.IsNullOrWhiteSpace(await _settings.GetAsync(DirectorySettingsKeys.ApiKey, defaultValue: null, ct)),
            HasApiSecretStored       = !string.IsNullOrWhiteSpace(await _settings.GetAsync(DirectorySettingsKeys.ApiSecret, defaultValue: null, ct))
        };
        return View(form);
    }

    [HttpPost("setup")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Setup(SetupForm form, CancellationToken ct)
    {
        if (form.Source is not ("Database" or "Api" or "Manual" or "Disabled"))
        {
            ModelState.AddModelError(nameof(form.Source), "Source must be Database / Api / Manual / Disabled.");
            return View(form);
        }
        await _settings.SetAsync(DirectorySettingsKeys.Source, form.Source, cancellationToken: ct);

        if (!string.IsNullOrWhiteSpace(form.DatabaseConnectionString))
            await _settings.SetAsync(DirectorySettingsKeys.DatabaseConnectionString, form.DatabaseConnectionString, isSecret: true, cancellationToken: ct);
        if (!string.IsNullOrWhiteSpace(form.ApiKey))
            await _settings.SetAsync(DirectorySettingsKeys.ApiKey, form.ApiKey, isSecret: true, cancellationToken: ct);
        if (!string.IsNullOrWhiteSpace(form.ApiSecret))
            await _settings.SetAsync(DirectorySettingsKeys.ApiSecret, form.ApiSecret, isSecret: true, cancellationToken: ct);

        await _settings.SetAsync(DirectorySettingsKeys.DatabaseStudentsView,  form.DatabaseStudentsView,  cancellationToken: ct);
        await _settings.SetAsync(DirectorySettingsKeys.DatabaseEmployeesView, form.DatabaseEmployeesView, cancellationToken: ct);
        await _settings.SetAsync(DirectorySettingsKeys.ApiBaseUrl,            form.ApiBaseUrl,            cancellationToken: ct);
        await _settings.SetAsync(DirectorySettingsKeys.ApiPageSize,           form.ApiPageSize.ToString(), cancellationToken: ct);
        await _settings.SetAsync(DirectorySettingsKeys.SyncIntervalMinutes,   form.SyncIntervalMinutes.ToString(), cancellationToken: ct);
        await _settings.SetAsync(DirectorySettingsKeys.SnapshotMode,          form.SnapshotMode, cancellationToken: ct);
        await _settings.SetAsync(DirectorySettingsKeys.MaxFailuresBeforeAlert,form.MaxFailuresBeforeAlert.ToString(), cancellationToken: ct);
        await _settings.SetAsync(DirectorySettingsKeys.AlertEmail,            form.AlertEmail, cancellationToken: ct);
        await _settings.SetAsync(DirectorySettingsKeys.AlertPhone,            form.AlertPhone, cancellationToken: ct);

        TempData["Flash.Success"] = "Directory source updated.";
        return RedirectToAction(nameof(Index));
    }

    // ─── 3. Sync triggers ─────────────────────────────────────────────────

    [HttpPost("sync-now")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SyncNow(CancellationToken ct)
    {
        var summary = await _sync.SyncCurrentTenantAsync(ct);
        TempData["Flash.Success"] = summary is null
            ? "No source configured — set Directory.Source to Database / Api or upload a CSV."
            : $"Sync completed: students +{summary.StudentsAdded}/~{summary.StudentsUpdated}/-{summary.StudentsDisabled}; employees +{summary.EmployeesAdded}/~{summary.EmployeesUpdated}/-{summary.EmployeesDisabled}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("upload-students")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> UploadStudents(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            TempData["Flash.Error"] = "Please choose a CSV file.";
            return RedirectToAction(nameof(Index));
        }
        try
        {
            await using var stream = file.OpenReadStream();
            var students = DirectoryCsvParser.ParseStudents(stream);
            var delta = new DirectoryDelta(students, Array.Empty<DirectoryEmployee>(), DateTime.UtcNow, IsFullSnapshot: true);
            var summary = await _sync.IngestAsync(delta, "Manual.Csv.Students", ct);
            TempData["Flash.Success"] = $"Student CSV ingested: +{summary.StudentsAdded}/~{summary.StudentsUpdated}/-{summary.StudentsDisabled}.";
        }
        catch (Exception ex) { TempData["Flash.Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("upload-employees")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadEmployees(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            TempData["Flash.Error"] = "Please choose a CSV file.";
            return RedirectToAction(nameof(Index));
        }
        try
        {
            await using var stream = file.OpenReadStream();
            var employees = DirectoryCsvParser.ParseEmployees(stream);
            var delta = new DirectoryDelta(Array.Empty<DirectoryStudent>(), employees, DateTime.UtcNow, IsFullSnapshot: true);
            var summary = await _sync.IngestAsync(delta, "Manual.Csv.Employees", ct);
            TempData["Flash.Success"] = $"Employee CSV ingested: +{summary.EmployeesAdded}/~{summary.EmployeesUpdated}/-{summary.EmployeesDisabled}.";
        }
        catch (Exception ex) { TempData["Flash.Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    // ─── 4. Runs + roster pages ───────────────────────────────────────────

    [HttpGet("runs")]
    public async Task<IActionResult> Runs([FromQuery] int take = 100, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 10, 500);
        var runs = await _query.GetRecentRunsAsync(take, ct);
        return View(runs);
    }

    public sealed record RosterPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, string? Q);

    [HttpGet("students.csv")]
    public async Task<IActionResult> ExportStudentsCsv(CancellationToken ct)
    {
        var rows = await _query.ListAllActiveStudentsAsync(ct);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("ExternalId,CardIdentifier,Name,Gender,ContactNo,Program,Class,Section,Session,IsActive,SyncedAtUtc");
        foreach (var s in rows)
        {
            sb.Append(C(s.ExternalId)).Append(',')
              .Append(C(s.CardIdentifier)).Append(',')
              .Append(C(s.Name)).Append(',')
              .Append(C(s.Gender)).Append(',')
              .Append(C(s.ContactNo)).Append(',')
              .Append(C(s.Program)).Append(',')
              .Append(C(s.Class)).Append(',')
              .Append(C(s.Section)).Append(',')
              .Append(C(s.Session)).Append(',')
              .Append(s.IsActive).Append(',')
              .Append(s.SyncedAtUtc.ToString("o")).AppendLine();
        }
        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv",
            $"students_{DateTime.UtcNow:yyyyMMddHHmm}.csv");
    }

    [HttpGet("employees.csv")]
    public async Task<IActionResult> ExportEmployeesCsv(CancellationToken ct)
    {
        var rows = await _query.ListAllActiveEmployeesAsync(ct);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("ExternalId,CardIdentifier,Name,Gender,ContactNo,Designation,EmployeeType,IsActive,SyncedAtUtc");
        foreach (var e in rows)
        {
            sb.Append(C(e.ExternalId)).Append(',')
              .Append(C(e.CardIdentifier)).Append(',')
              .Append(C(e.Name)).Append(',')
              .Append(C(e.Gender)).Append(',')
              .Append(C(e.ContactNo)).Append(',')
              .Append(C(e.Designation)).Append(',')
              .Append(C(e.EmployeeType)).Append(',')
              .Append(e.IsActive).Append(',')
              .Append(e.SyncedAtUtc.ToString("o")).AppendLine();
        }
        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv",
            $"employees_{DateTime.UtcNow:yyyyMMddHHmm}.csv");
    }

    private static string C(string? v)
    {
        if (string.IsNullOrEmpty(v)) return string.Empty;
        if (v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return v;
        return "\"" + v.Replace("\"", "\"\"") + "\"";
    }

    [HttpGet("students")]
    public async Task<IActionResult> Students(int page = 1, int pageSize = 50, string? q = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 10, 200);
        var paged = await _query.SearchStudentsAsync(page, pageSize, q, ct);
        return View(new RosterPage<Student>(paged.Items, paged.Page, paged.PageSize, paged.TotalCount, paged.Query));
    }

    [HttpGet("employees")]
    public async Task<IActionResult> Employees(int page = 1, int pageSize = 50, string? q = null, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 10, 200);
        var paged = await _query.SearchEmployeesAsync(page, pageSize, q, ct);
        return View(new RosterPage<Employee>(paged.Items, paged.Page, paged.PageSize, paged.TotalCount, paged.Query));
    }
}
