// =============================================================================
// IReportScheduleAdminService  (Platform.Application.Abstractions.Reporting)
// -----------------------------------------------------------------------------
// Admin-side CRUD + "Run now" over ReportSchedule rows. Used by
// /admin/report-schedules so the controller never touches IAppDbContext
// directly (ADR 0004).
// =============================================================================

using Platform.Application.Results;
using Platform.Domain.Reporting;

namespace Platform.Application.Abstractions.Reporting;

public sealed record ReportScheduleInput(
    string ReportKey, string DisplayName, string Recurrence,
    int? HourOfDay, int? Minute, int? DayOfWeek, int? DayOfMonth, int? IntervalHours,
    string Format, string? ParametersJson, string Recipients, bool IsEnabled,
    string? CreatedBy);

public sealed record ReportScheduleRunOutcome(bool Success, string? Detail, int RecipientCount);

public interface IReportScheduleAdminService
{
    Task<IReadOnlyList<ReportSchedule>> ListAsync(CancellationToken cancellationToken = default);
    Task<ReportSchedule?> GetByIdAsync(int scheduleId, CancellationToken cancellationToken = default);

    Task<Result<ReportSchedule>> CreateAsync(ReportScheduleInput input, CancellationToken cancellationToken = default);
    Task<Result<ReportSchedule>> UpdateAsync(int scheduleId, ReportScheduleInput input, CancellationToken cancellationToken = default);

    Task<Result> ToggleEnabledAsync(int scheduleId, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(int scheduleId, CancellationToken cancellationToken = default);

    /// <summary>Run the schedule immediately (same code path the background worker uses).</summary>
    Task<ReportScheduleRunOutcome> RunNowAsync(int scheduleId, CancellationToken cancellationToken = default);
}
