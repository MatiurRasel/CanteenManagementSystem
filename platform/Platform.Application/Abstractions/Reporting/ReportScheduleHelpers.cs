// =============================================================================
// ReportScheduleOccurrence / ReportParameterTokens  (Platform.Application.Abstractions.Reporting)
// -----------------------------------------------------------------------------
// Pure helpers — no I/O. Compute the next firing time for a Recurrence pattern,
// and resolve magic tokens ("@TODAY", "@MONTH_START") inside a parameter JSON
// blob so a single schedule row can mean "yesterday's revenue" or
// "this-week's audit" without re-editing the JSON every day.
//
// Why pure: the background service runs in a different scope per tick, so the
// next-occurrence logic must be a side-effect-free function of (now, schedule).
// =============================================================================

namespace Platform.Application.Abstractions.Reporting;

public static class ReportScheduleOccurrence
{
    /// <summary>
    /// Next firing time strictly AFTER <paramref name="afterUtc"/>. If the
    /// schedule has invalid components for its recurrence (e.g. Weekly without
    /// DayOfWeek) we fall back to 24 h from now so a misconfigured row keeps
    /// the loop alive but is observably late in the admin UI.
    /// </summary>
    public static DateTime ComputeNext(string recurrence, int? hourOfDay, int? minute,
                                       int? dayOfWeek, int? dayOfMonth, int? intervalHours,
                                       DateTime afterUtc)
    {
        var hour = hourOfDay ?? 23;
        var min  = minute ?? 0;
        hour = Math.Clamp(hour, 0, 23);
        min  = Math.Clamp(min,  0, 59);

        switch (recurrence?.ToLowerInvariant())
        {
            case "hourly":
                var step = Math.Max(1, intervalHours ?? 1);
                var baseUtc = new DateTime(afterUtc.Year, afterUtc.Month, afterUtc.Day, afterUtc.Hour, 0, 0, DateTimeKind.Utc);
                var candidate = baseUtc.AddHours(step);
                while (candidate <= afterUtc) candidate = candidate.AddHours(step);
                return candidate;

            case "weekly":
                var targetDow = (int)Math.Clamp(dayOfWeek ?? 1, 0, 6);
                var nowDow    = (int)afterUtc.DayOfWeek;
                var daysAhead = (targetDow - nowDow + 7) % 7;
                var w         = afterUtc.Date.AddDays(daysAhead).AddHours(hour).AddMinutes(min);
                w = DateTime.SpecifyKind(w, DateTimeKind.Utc);
                if (w <= afterUtc) w = w.AddDays(7);
                return w;

            case "monthly":
                var year  = afterUtc.Year;
                var month = afterUtc.Month;
                var dayInMonth = Math.Clamp(dayOfMonth ?? 1, 1, 28);   // safe default
                var actualDay = Math.Min(dayInMonth, DateTime.DaysInMonth(year, month));
                var m = new DateTime(year, month, actualDay, hour, min, 0, DateTimeKind.Utc);
                if (m <= afterUtc)
                {
                    month++; if (month > 12) { month = 1; year++; }
                    actualDay = Math.Min(dayInMonth, DateTime.DaysInMonth(year, month));
                    m = new DateTime(year, month, actualDay, hour, min, 0, DateTimeKind.Utc);
                }
                return m;

            case "daily":
            default:
                var d = new DateTime(afterUtc.Year, afterUtc.Month, afterUtc.Day, hour, min, 0, DateTimeKind.Utc);
                if (d <= afterUtc) d = d.AddDays(1);
                return d;
        }
    }
}

/// <summary>
/// Resolves dynamic tokens inside a schedule's ParametersJson so a single row
/// can mean "yesterday" / "this month so far" / "last 7 days" without rewriting
/// the JSON every run.
/// </summary>
public static class ReportParameterTokens
{
    /// <summary>Replace magic tokens with concrete ISO dates relative to <paramref name="nowUtc"/>.</summary>
    public static string Expand(string? parametersJson, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(parametersJson)) return "{}";
        var today        = nowUtc.Date;
        var yesterday    = today.AddDays(-1);
        var weekStart    = today.AddDays(-(int)today.DayOfWeek);
        var monthStart   = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var weekAgo      = today.AddDays(-7);
        var thirtyDayAgo = today.AddDays(-30);

        return parametersJson!
            .Replace("@TODAY",         today.ToString("yyyy-MM-dd"))
            .Replace("@YESTERDAY",     yesterday.ToString("yyyy-MM-dd"))
            .Replace("@WEEK_START",    weekStart.ToString("yyyy-MM-dd"))
            .Replace("@WEEK_AGO",      weekAgo.ToString("yyyy-MM-dd"))
            .Replace("@MONTH_START",   monthStart.ToString("yyyy-MM-dd"))
            .Replace("@THIRTY_DAY_AGO",thirtyDayAgo.ToString("yyyy-MM-dd"));
    }
}
