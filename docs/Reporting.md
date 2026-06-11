# Reporting & Printing Framework

A reusable framework that lives in **Platform** and is shared by every SaaS
product on this codebase (Canteen today; Property Rent and Hospital/Clinic
next). 100 % free OSS — no paid libraries, no revenue caps.

---

## Library choices

| Format | Library | Licence | Why |
|---|---|---|---|
| PDF   | **PdfSharpCore** | MIT | True cross-platform, MIT, no revenue cap |
| XLSX  | **ClosedXML**    | MIT | De-facto Excel for .NET, ergonomic API |
| CSV   | built-in `StringBuilder` | — | Zero dep, Excel-friendly (UTF-8 BOM + comment header) |
| HTML  | inline Razor / string template | — | Self-contained, emailable |
| JSON  | `System.Text.Json` | — | For warehouse / BI pull, debugging |
| Thermal (ESC/POS) | custom in-house | MIT (ours) | Already shipped in `EscPosNetworkPrinter` |

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              IReport (one per report)                       │
│   Key · DisplayName · Group · RequiredPermissions · SupportedFormats        │
│   ParameterType (POCO) · GenerateAsync() → ReportDocument                   │
└────────────────────────────┬────────────────────────────────────────────────┘
                             │
                             ▼
                ┌─────────────────────────┐
                │     ReportDocument      │   format-agnostic
                │  Title / Subtitle       │
                │  Summary (KPIs)         │
                │  Sections (tables)      │
                └────────────┬────────────┘
                             │
        ┌────────────┬───────┴───────┬────────────┬────────────┐
        ▼            ▼               ▼            ▼            ▼
   ┌─────────┐  ┌─────────┐    ┌─────────┐  ┌──────────┐ ┌────────┐
   │   Csv   │  │  Xlsx   │    │   Pdf   │  │   Html   │ │  Json  │
   │Renderer │  │Renderer │    │Renderer │  │Renderer  │ │Renderer│
   │(builtin)│  │ClosedXML│    │PdfSharp │  │ (Razor)  │ │ STJ    │
   └─────────┘  └─────────┘    └─────────┘  └──────────┘ └────────┘
                             │
                             ▼
                   ┌──────────────────┐
                   │ IReportDispatcher│  ← binds params, picks renderer
                   └──────────────────┘
                             │
                             ▼
                   ┌──────────────────┐
                   │ IReportRegistry  │  ← discovers all IReport in DI
                   └──────────────────┘
```

## Public types (Platform.Application.Abstractions.Reporting)

| Type | Role |
|---|---|
| `ReportFormat` (enum) | Csv / Xlsx / Pdf / Html / Json |
| `ReportFormatExtensions` | `.MimeType()` / `.Extension()` |
| `ReportDocument` | Format-agnostic shape: title + summary KPIs + sections |
| `ReportSection` | Columns + rows (or `FreeTextHtml`) + `TotalsRow` flag |
| `ReportColumn` | Header + `ReportColumnType` + format string + width % + right-align |
| `ReportColumnType` | Text / Number / Money / Percent / Date / DateTime / Boolean |
| `ReportKpi` | Label / Value / Hint — appears in summary block |
| `IReport` | Untyped contract used by registry/dispatcher |
| `IReport<TParameters>` | Typed contract products implement |
| `IReportRenderer` | One per format |
| `IReportRegistry` | Lists every `IReport` in DI |
| `IReportDispatcher` | `RunAsync(key, formData, format)` → bytes + MIME + filename |
| `DateRangeParameters` | Common base — `From` / `To` |

## Boot wiring

```csharp
// Program.cs — once per product
builder.Services.AddPlatformReporting();
```

`AddPlatformReporting()` registers:

```csharp
services.AddSingleton<IReportRenderer, CsvReportRenderer>();
services.AddSingleton<IReportRenderer, XlsxReportRenderer>();
services.AddSingleton<IReportRenderer, PdfReportRenderer>();
services.AddSingleton<IReportRenderer, HtmlReportRenderer>();
services.AddSingleton<IReportRenderer, JsonReportRenderer>();
services.AddScoped<IReportRegistry, ReportRegistry>();
services.AddScoped<IReportDispatcher, ReportDispatcher>();
```

Each product then registers **one line per report**:

```csharp
// CanteenManagementSystem.Infrastructure.DependencyInjection
services.AddScoped<IReport, DailyCollectionReport>();
services.AddScoped<IReport, PopularItemsReport>();
services.AddScoped<IReport, AuditTrailReport>();
```

---

## Adding a new report (for sister projects)

### 1. Write the parameter POCO

```csharp
public sealed class TenantOccupancyParameters : DateRangeParameters
{
    public string? Building { get; set; }     // optional filter
}
```

### 2. Implement `IReport<TParameters>`

```csharp
public sealed class TenantOccupancyReport : IReport<TenantOccupancyParameters>
{
    public string Key                                   => "rent-tenant-occupancy";
    public string DisplayName                           => "Tenant occupancy";
    public string? Description                          => "Occupied units vs vacant.";
    public string Group                                 => "Operations";
    public IReadOnlyList<string> RequiredPermissions    => new[] { "Reports.View" };
    public IReadOnlyList<ReportFormat> SupportedFormats =>
        new[] { ReportFormat.Pdf, ReportFormat.Xlsx, ReportFormat.Csv };
    public Type ParameterType                           => typeof(TenantOccupancyParameters);

    private readonly IAppDbContext _db;
    public TenantOccupancyReport(IAppDbContext db) => _db = db;

    public Task<ReportDocument> GenerateAsync(object? parameters, CancellationToken ct)
        => GenerateAsync((parameters as TenantOccupancyParameters) ?? new(), ct);

    public async Task<ReportDocument> GenerateAsync(TenantOccupancyParameters p, CancellationToken ct)
    {
        var (from, to) = p.NormalizedUtc();
        // ... query DB, build rows ...
        return new ReportDocument
        {
            Title    = "Tenant occupancy",
            Subtitle = $"{from:yyyy-MM-dd} → {to:yyyy-MM-dd}",
            Summary  = new[]
            {
                new ReportKpi("Occupied", occupied.ToString()),
                new ReportKpi("Vacant",   vacant.ToString())
            },
            Sections = new[] { new ReportSection
            {
                Heading = "By unit",
                Columns = new[]
                {
                    new ReportColumn("Unit",     ReportColumnType.Text,     WidthPercent: 30),
                    new ReportColumn("Tenant",   ReportColumnType.Text,     WidthPercent: 40),
                    new ReportColumn("Rent",     ReportColumnType.Money,    WidthPercent: 15, AlignRight: true),
                    new ReportColumn("Move-in",  ReportColumnType.Date,     WidthPercent: 15)
                },
                Rows = rows
            }}
        };
    }
}
```

### 3. Register in DI

```csharp
services.AddScoped<IReport, TenantOccupancyReport>();
```

That's it. The admin `/admin/reports-catalog` page picks it up automatically, the form-builder auto-generates inputs for every public property on `TenantOccupancyParameters`, and clicking "Generate" downloads the file in the chosen format.

---

## How rendering maps onto each format

| `ReportColumnType` | CSV | XLSX | PDF | HTML |
|---|---|---|---|---|
| Text | as-is | `string` cell | left-aligned | `<td>` |
| Number | `N0` invariant | numeric, `#,##0` | right-aligned | `<td class="num">` |
| Money | `N2` invariant | numeric, `#,##0.00` | right-aligned | `<td class="num">` |
| Percent | `P2` invariant | numeric, `0.00%` | right-aligned | `<td class="num">` |
| Date | `yyyy-MM-dd` | date cell, `yyyy-mm-dd` | as-is | as-is |
| DateTime | `yyyy-MM-dd HH:mm:ss` | datetime cell | as-is | as-is |
| Boolean | `TRUE`/`FALSE` | boolean cell | TRUE/FALSE | TRUE/FALSE |

`TotalsRow = true` → last row is bold + ruled top in PDF/XLSX/HTML.

`FreeTextHtml` on a section overrides the table grid — useful for compliance reports that need a paragraph of text.

---

## Admin surface

| Route | Persona | Effect |
|---|---|---|
| `GET /admin/reports-catalog` | Auditor / TenantAdmin | Lists every IReport grouped by `Group` |
| `GET /admin/reports-catalog/{key}` | Auditor / TenantAdmin | Auto-built form using `ParameterType` reflection + format radios |
| `GET /admin/reports-catalog/{key}/download?format=Pdf&...` | Auditor / TenantAdmin | Runs the report and streams the file |

The legacy `/admin/reports` (chart dashboard) is preserved — it remains the
quick-glance dashboard. The new catalog is the **downloadable** surface.

---

## Scheduled distribution (shipped)

Reports can fire automatically and email themselves as attachments. One row
per schedule in the `ReportSchedules` table.

### Pieces

| File | Role |
|---|---|
| `Platform.Domain.Reporting.ReportSchedule` | The persisted row |
| `Platform.Application.Abstractions.Reporting.ReportScheduleOccurrence` | Pure `ComputeNext(...)` helper (no I/O) |
| `Platform.Application.Abstractions.Reporting.ReportParameterTokens` | Expands `@TODAY`, `@WEEK_AGO`, etc. before each run |
| `Platform.Application.Abstractions.Reporting.IReportDistributor` | Delivery seam — email today, S3/Slack future |
| `Platform.Infrastructure.Reporting.SmtpReportDistributor` | SMTP with attachment; reads `Notifications.Email.*` settings |
| `Platform.Infrastructure.Reporting.ReportSchedulerBackgroundService` | Hosted; ticks every 1 min, scopes per tenant, picks due rows, runs, emails, stamps `LastRunAtUtc` + `NextRunAtUtc` |
| `Controllers/ReportSchedulesController` | CRUD + "Run now" |
| Views: `Index.cshtml`, `Edit.cshtml` | Admin UI under `/admin/report-schedules` |

### Recurrence patterns

| Recurrence | Fields used | Semantics |
|---|---|---|
| `Daily`   | HourOfDay, Minute                       | Once per day at the chosen UTC time |
| `Weekly`  | HourOfDay, Minute, DayOfWeek (0=Sun)    | Once per week |
| `Monthly` | HourOfDay, Minute, DayOfMonth (1-31)    | Clamped to last-day-of-month |
| `Hourly`  | IntervalHours                           | Every N hours starting from the next hour boundary |

### Magic parameter tokens

Inside `ParametersJson`, the scheduler replaces these BEFORE binding to the
report's parameter POCO:

| Token | Expands to |
|---|---|
| `@TODAY`           | today (UTC), `yyyy-MM-dd` |
| `@YESTERDAY`       | yesterday |
| `@WEEK_START`      | Sunday of this week |
| `@WEEK_AGO`        | today − 7 days |
| `@MONTH_START`     | first of this month |
| `@THIRTY_DAY_AGO`  | today − 30 days |

Example — "Email yesterday's collection at 23:00 UTC daily":

```json
{ "From": "@YESTERDAY", "To": "@YESTERDAY" }
```

### Failure handling

- Render failure → `LastRunStatus = "Failed"`, `LastError = ex.Message` (truncated).
- SMTP failure → same. `NextRunAtUtc` is recomputed regardless, so transient outages don't permanently disable.
- Misconfigured rows (bad report key, malformed JSON) keep ticking until fixed — the admin UI shows the error inline.
- Tenant-level failure isolation: one tenant's broken schedule can't stop another's runs.

### Email body

The current SMTP distributor emits a tiny HTML body (subject + greeting +
"your report is attached") and attaches the rendered bytes with the right
MIME type + suggested filename. Tenant admins set sender/credentials in
the existing `Notifications.Email.*` keys.

---

## Reports shipped today

| Key | Group | Description |
|---|---|---|
| `canteen-daily-collection` | Sales | Revenue / orders / refunds per day across a range |
| `canteen-popular-items` | Sales | Top N items by units sold |
| `platform-audit-trail` | Compliance | All audit entries (filterable by date + action prefix) |

Each is one class in `src/CanteenManagementSystem.Infrastructure/Reports/Reports/`.

---

## Why this design

| Decision | Trade-off |
|---|---|
| Hand-rolled abstractions (vs. picking a heavyweight report framework like FastReport) | More code to maintain in-house, but zero licence risk + 100 % portable into commercial products |
| `ReportDocument` is the boundary, not raw `IEnumerable<T>` | Renderers stay product-agnostic; adding a new renderer (eg. ODS, RTF) is one class |
| Parameters bound from `Dictionary<string, string?>` via JSON round-trip | One code path handles admin form, cron trigger, and API call uniformly |
| Renderers are **singleton** | Stateless, allocation-light |
| Registry resolved per scope (not singleton) | So `services.AddScoped<IReport, ...>` correctly captures per-request DI (DbContext / tenant) |
