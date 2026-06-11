// =============================================================================
// ReportScheduleAdminService  (Platform.Infrastructure.Reporting)
// -----------------------------------------------------------------------------
// IReportScheduleAdminService impl. Uses IUnitOfWork for ReportSchedule writes
// + IReportDispatcher + IReportDistributor for the "Run now" sync path.
// ADR 0004 — no IAppDbContext.
// =============================================================================

using Platform.Application.Abstractions.Reporting;
using Platform.Application.Persistence;
using Platform.Application.Results;
using Platform.Domain.Reporting;

namespace Platform.Infrastructure.Reporting;

public sealed class ReportScheduleAdminService : IReportScheduleAdminService
{
    private readonly IUnitOfWork _uow;
    private readonly IReportDispatcher _dispatcher;
    private readonly IReportDistributor _distributor;

    public ReportScheduleAdminService(IUnitOfWork uow, IReportDispatcher dispatcher, IReportDistributor distributor)
    {
        _uow = uow; _dispatcher = dispatcher; _distributor = distributor;
    }

    private IRepository<ReportSchedule> Repo => _uow.Repository<ReportSchedule>();

    public async Task<IReadOnlyList<ReportSchedule>> ListAsync(CancellationToken ct = default)
    {
        var rows = await Repo.ListAsync(ct);
        return rows.OrderBy(s => s.DisplayName).ToList();
    }

    public Task<ReportSchedule?> GetByIdAsync(int scheduleId, CancellationToken ct = default)
        => Repo.FirstOrDefaultAsync(s => s.ScheduleId == scheduleId, ct);

    public async Task<Result<ReportSchedule>> CreateAsync(ReportScheduleInput input, CancellationToken ct = default)
    {
        var row = new ReportSchedule
        {
            ReportKey      = input.ReportKey,
            DisplayName    = input.DisplayName,
            Recurrence     = input.Recurrence,
            HourOfDay      = input.HourOfDay,
            Minute         = input.Minute,
            DayOfWeek      = input.Recurrence == "Weekly"  ? input.DayOfWeek  : null,
            DayOfMonth     = input.Recurrence == "Monthly" ? input.DayOfMonth : null,
            IntervalHours  = input.Recurrence == "Hourly"  ? input.IntervalHours : null,
            Format         = input.Format,
            ParametersJson = input.ParametersJson,
            Recipients     = input.Recipients,
            IsEnabled      = input.IsEnabled,
            NextRunAtUtc   = ReportScheduleOccurrence.ComputeNext(
                                input.Recurrence, input.HourOfDay, input.Minute,
                                input.DayOfWeek, input.DayOfMonth, input.IntervalHours, DateTime.UtcNow),
            CreatedBy      = input.CreatedBy,
            CreatedAtUtc   = DateTime.UtcNow
        };
        await Repo.AddAsync(row, ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Success(row);
    }

    public async Task<Result<ReportSchedule>> UpdateAsync(int scheduleId, ReportScheduleInput input, CancellationToken ct = default)
    {
        var row = await Repo.FirstOrDefaultAsync(s => s.ScheduleId == scheduleId, ct);
        if (row is null) return Result.Failure<ReportSchedule>(Error.NotFound("Schedule not found."));

        row.ReportKey      = input.ReportKey;
        row.DisplayName    = input.DisplayName;
        row.Recurrence     = input.Recurrence;
        row.HourOfDay      = input.HourOfDay;
        row.Minute         = input.Minute;
        row.DayOfWeek      = input.Recurrence == "Weekly"  ? input.DayOfWeek  : null;
        row.DayOfMonth     = input.Recurrence == "Monthly" ? input.DayOfMonth : null;
        row.IntervalHours  = input.Recurrence == "Hourly"  ? input.IntervalHours : null;
        row.Format         = input.Format;
        row.ParametersJson = input.ParametersJson;
        row.Recipients     = input.Recipients;
        row.IsEnabled      = input.IsEnabled;
        row.UpdatedAtUtc   = DateTime.UtcNow;
        row.NextRunAtUtc   = ReportScheduleOccurrence.ComputeNext(
            row.Recurrence, row.HourOfDay, row.Minute,
            row.DayOfWeek, row.DayOfMonth, row.IntervalHours, DateTime.UtcNow);
        Repo.Update(row);
        await _uow.SaveChangesAsync(ct);
        return Result.Success(row);
    }

    public async Task<Result> ToggleEnabledAsync(int scheduleId, CancellationToken ct = default)
    {
        var row = await Repo.FirstOrDefaultAsync(s => s.ScheduleId == scheduleId, ct);
        if (row is null) return Result.Failure(Error.NotFound("Schedule not found."));
        row.IsEnabled = !row.IsEnabled;
        row.UpdatedAtUtc = DateTime.UtcNow;
        Repo.Update(row);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(int scheduleId, CancellationToken ct = default)
    {
        var row = await Repo.FirstOrDefaultAsync(s => s.ScheduleId == scheduleId, ct);
        if (row is null) return Result.Failure(Error.NotFound("Schedule not found."));
        Repo.Remove(row);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<ReportScheduleRunOutcome> RunNowAsync(int scheduleId, CancellationToken ct = default)
    {
        var row = await Repo.FirstOrDefaultAsync(s => s.ScheduleId == scheduleId, ct);
        if (row is null) return new ReportScheduleRunOutcome(false, "Schedule not found.", 0);

        try
        {
            var expanded = ReportParameterTokens.Expand(row.ParametersJson, DateTime.UtcNow);
            var dict = ParseToFormData(expanded);
            if (!Enum.TryParse<ReportFormat>(row.Format, true, out var format)) format = ReportFormat.Pdf;
            var rendered = await _dispatcher.RunAsync(row.ReportKey, dict, format, ct);
            var recipients = (row.Recipients ?? string.Empty)
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var dist = await _distributor.EmailAsync(
                recipients,
                $"[Canteen] {row.DisplayName} — {DateTime.UtcNow:yyyy-MM-dd}",
                $"<p>Hi,</p><p>Your scheduled report <b>{row.DisplayName}</b> is attached.</p>",
                rendered, ct);

            row.LastRunAtUtc  = DateTime.UtcNow;
            row.LastRunStatus = dist.Success ? "Success" : "Failed";
            row.LastError     = dist.Success ? null : dist.Detail;
            Repo.Update(row);
            await _uow.SaveChangesAsync(ct);
            return new ReportScheduleRunOutcome(dist.Success, dist.Detail, recipients.Length);
        }
        catch (Exception ex)
        {
            row.LastRunAtUtc  = DateTime.UtcNow;
            row.LastRunStatus = "Failed";
            row.LastError     = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            Repo.Update(row);
            await _uow.SaveChangesAsync(ct);
            return new ReportScheduleRunOutcome(false, ex.Message, 0);
        }
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
        catch { /* swallowed — dispatcher uses defaults */ }
        return dict;
    }
}
