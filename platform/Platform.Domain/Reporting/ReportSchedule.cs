// =============================================================================
// ReportSchedule  (Platform.Domain.Reporting)  Table: ReportSchedules
// -----------------------------------------------------------------------------
// Persisted cron-style schedule for an IReport. Tenant-scoped so every product
// admin manages their own schedules; the background service crosses tenants
// when ticking (with the global query filter off).
//
// FIELDS
//   ReportKey         "canteen-daily-collection"  (matches IReport.Key)
//   Recurrence        Daily | Weekly | Monthly | Hourly
//   HourOfDay/Minute  for Daily / Weekly / Monthly (0-23 / 0-59)
//   DayOfWeek         0=Sun … 6=Sat   (Weekly only)
//   DayOfMonth        1-31            (Monthly only; clamped to last day-of-month)
//   IntervalHours     for Hourly (>=1)
//   Format            "Pdf" | "Xlsx" | "Csv" | "Html" | "Json"
//   ParametersJson    optional — overrides defaults. Supports the magic tokens:
//                       "@TODAY", "@YESTERDAY", "@WEEK_START", "@MONTH_START"
//                       resolved by the scheduler at run time.
//   Recipients        CSV of email addresses (the SmtpReportDistributor splits)
//
// AUDIT
//   LastRunAtUtc / NextRunAtUtc / LastRunStatus / LastError stamped by the
//   background service after every tick.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;

namespace Platform.Domain.Reporting;

[Table("ReportSchedules")]
public class ReportSchedule : IAggregateRoot, ITenantOwned
{
    [Key]
    public int ScheduleId { get; set; }

    [Required, StringLength(64)]
    public string ReportKey { get; set; } = string.Empty;

    [Required, StringLength(128)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>"Daily" | "Weekly" | "Monthly" | "Hourly".</summary>
    [Required, StringLength(16)]
    public string Recurrence { get; set; } = "Daily";

    public int? HourOfDay { get; set; }
    public int? Minute { get; set; }
    public int? DayOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public int? IntervalHours { get; set; }

    [Required, StringLength(16)]
    public string Format { get; set; } = "Pdf";

    [StringLength(2000)]
    public string? ParametersJson { get; set; }

    [Required, StringLength(1000)]
    public string Recipients { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public DateTime? LastRunAtUtc { get; set; }
    public DateTime? NextRunAtUtc { get; set; }

    [StringLength(16)]
    public string? LastRunStatus { get; set; }

    [StringLength(2000)]
    public string? LastError { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
