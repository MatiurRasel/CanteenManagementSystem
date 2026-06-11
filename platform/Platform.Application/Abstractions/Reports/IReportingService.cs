// =============================================================================
// IReportingService  (Application)
// -----------------------------------------------------------------------------
// Read-only aggregations that power the admin dashboards and downloadable
// CSV reports. All queries are tenant-scoped via the global EF query filter
// (Phase 2) and cached in ICacheService.
//
// PERFORMANCE STRATEGY
//   * Aggregation queries use GROUP BY in EF so the database does the work.
//   * Results are cached for 60s — dashboards auto-refresh every minute, so
//     the cache window covers the bulk of repeat traffic.
//   * Output cache policy "Reports" attaches to the controllers below to
//     short-circuit responses when payloads are identical (varies on date).
// =============================================================================

namespace Platform.Application.Abstractions.Reports;

public interface IReportingService
{
    Task<DailyCollectionReport>   GetDailyCollectionAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PopularItemRow>> GetPopularItemsAsync(DateTime fromDate, DateTime toDate, int top = 10, CancellationToken cancellationToken = default);
    Task<WastageReport>           GetWastageAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PeakHourRow>> GetPeakHoursAsync(DateTime targetDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DepartmentSpendRow>> GetDepartmentSpendAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    Task<byte[]>                  ExportDailyCollectionCsvAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
}

public sealed record DailyCollectionReport(decimal Total, int OrderCount, IReadOnlyList<DailyCollectionRow> Days);
public sealed record DailyCollectionRow(DateTime Date, int Orders, decimal Revenue, decimal Refunded);
public sealed record PopularItemRow(string ItemName, int QuantitySold, decimal Revenue);
public sealed record WastageReport(decimal TotalWasteValue, IReadOnlyList<WastageRow> Rows);
public sealed record WastageRow(string ItemName, int InitialQuantity, int Sold, int Wasted, decimal WasteValue);
public sealed record PeakHourRow(int Hour, int Orders, decimal Revenue);
public sealed record DepartmentSpendRow(string Department, int Users, int Orders, decimal Spend);
