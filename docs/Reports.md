# Reports & Analytics

All endpoints are output-cached for 30 seconds *and* the inner aggregation
results are L1+L2 cached for 60 seconds. Heavy traffic hits the database at
most once per minute per (tenant, date-range).

## API

```
GET /api/v1/reports/daily-collection?from=2026-06-01&to=2026-06-30
GET /api/v1/reports/popular-items?from=…&to=…&top=10
GET /api/v1/reports/wastage?from=…&to=…
GET /api/v1/reports/peak-hours?date=2026-06-15
GET /api/v1/reports/department-spend?from=…&to=…
GET /api/v1/reports/daily-collection.csv?from=…&to=…
```

## Aggregations

* **Daily collection** — `GROUP BY OrderDate.Date` for orders, sums revenue
  vs refunded.
* **Popular items** — `GROUP BY FoodItem.ItemName` ordered by quantity desc.
* **Wastage** — `InitialQuantity - Sold` valued at item price.
* **Peak hours** — `GROUP BY OrderDate.Hour` for one date.
* **Department spend** — joins Orders with StudentInfo (ProgramName) and
  EmployeeInfo (EmployeeTypeName).

## CSV export

`/daily-collection.csv` reuses the same cached aggregation and emits UTF-8
text. Returned as `text/csv` with a download filename.

## Adding a new report

1. Add a method to `IReportingService`.
2. Implement in `ReportingService` with `_cache.GetOrSetAsync(...)`.
3. Expose in `ReportsController`. The `[OutputCache(PolicyName="Reports")]`
   on the controller applies automatically.
