# Directory sync

Per-tenant student / employee roster sync. The canteen counter reads ONLY the local `Students` / `Employees` tables — never crosses to the school SIS at scan time. A background worker (or admin-triggered "Sync now") pulls from the tenant's configured source.

---

## Pieces

| Layer | Component | Role |
|---|---|---|
| Application | `IDirectorySource`, `IDirectoryWriter`, `IDirectorySyncService`, `IDirectorySourceFactory`, DTOs | Platform-generic seams |
| Application | `DirectorySettingsKeys` | Canonical setting-key names ("Directory.\*") |
| Infrastructure | `DirectorySyncService` | Orchestrator (audit row + dispatch + circuit-breaker + alerts) |
| Infrastructure | `DirectorySourceFactory` | Reads `Directory.Source` per-tenant, builds the right source |
| Infrastructure | `DatabaseDirectorySource` | ADO.NET → school DB views (`vw_StudentInfo_Canteen` etc.) |
| Infrastructure | `ApiDirectorySource` | HMAC-signed HTTP → school portal API |
| Infrastructure | `ManualDirectorySource` | No-op (CSV upload path bypasses) |
| Infrastructure | `DirectorySyncBackgroundService` | Hosted; ticks every minute, runs due tenants |
| Canteen.Infrastructure | `CanteenDirectoryWriter` | Persists DTOs into `Students` / `Employees` |
| Canteen.Infrastructure | `DirectoryCsvParser` | State-machine CSV reader |
| Canteen.Presentation | `DirectoryAdminController` (REST) | Tenant-admin JSON API |
| Canteen.Presentation | `DirectoryAdminUiController` (MVC) | Tenant-admin Razor views |
| Canteen.Presentation | `TenantsAdminController` (MVC) | System-admin tenant onboarding |

## Boot wiring

```csharp
// CanteenManagementSystem.Infrastructure.DependencyInjection
services.AddScoped<IDirectoryWriter, CanteenDirectoryWriter>();
services.AddScoped<IDirectorySyncService, DirectorySyncService>();
services.AddScoped<IDirectorySourceFactory, DirectorySourceFactory>();
services.AddHttpClient("directory.api", c => c.Timeout = TimeSpan.FromSeconds(30));
services.AddHostedService<DirectorySyncBackgroundService>();
```

## Tenant settings keys (canonical)

```
Directory.Source                       "Database" | "Api" | "Manual" | "Disabled"  (default "Manual")
Directory.Database.ConnectionString    (encrypted)
Directory.Database.Provider            "SqlServer" (future-proofing)
Directory.Database.StudentsView        default "vw_StudentInfo_Canteen"
Directory.Database.EmployeesView       default "vw_EmployeeInfo_Canteen"
Directory.Api.BaseUrl
Directory.Api.Key                      (encrypted, public X-App-Key)
Directory.Api.Secret                   (encrypted, HMAC signing key)
Directory.Api.PageSize                 default 500
Directory.SyncIntervalMinutes          default 30
Directory.SnapshotMode                 "PerSync" | "Daily"
Directory.LastSyncAtUtc                ISO 8601, written on success
Directory.HealthStatus                 "Healthy" | "Failing" | "Disabled"
Directory.ConsecutiveFailures          int, reset on success
Directory.MaxFailuresBeforeAlert       default 3
Directory.AlertEmail / AlertPhone      where alerts route
```

## Lifecycle of one sync

```
Tick (every minute)
   ↓
Enumerate active Clients (IgnoreQueryFilters — crossing tenants by design)
   ↓
For each tenant:
   ├─ New DI scope; stamp ITenantContext via IMutableTenantContext.Resolve()
   ├─ Read HealthStatus  → skip if "Disabled"
   ├─ Read LastSyncAtUtc + SyncIntervalMinutes → skip if not due
   ├─ Resolve IDirectorySource via factory (Source key → Database/Api/Manual)
   ├─ DirectorySyncService.SyncCurrentTenantAsync(ct)
   │     ├─ INSERT DirectorySyncRun (Status="Running") → SaveChanges
   │     ├─ source.FetchAsync(sinceUtc) → DirectoryDelta
   │     ├─ writer.WriteAsync(delta) → DirectoryWriteResult
   │     │     └─ hash-compare upsert per row; soft-disable if IsFullSnapshot
   │     ├─ UPDATE run (Status=Success, counts) → SaveChanges
   │     └─ On success:  write Directory.LastSyncAtUtc, reset failure counters, HealthStatus="Healthy"
   │        On failure:  write run as "Failed", increment ConsecutiveFailures,
   │                     if crosses MaxFailuresBeforeAlert → HealthStatus="Failing"
   │                     + send notification on first crossing only (no spam)
   └─ Exceptions inside one tenant don't break others (caught + logged)
```

## REST surface (under `/api/v1/admin/directory/*`, policy `TenantAdmin`)

| Verb | Path | Purpose |
|---|---|---|
| GET  | `/config`            | Current source config (secrets masked as `***`) |
| PUT  | `/config`            | Update source + credentials (only overwrite secrets when caller sends non-placeholder) |
| POST | `/sync-now`          | Trigger an immediate pull sync — returns run summary |
| POST | `/students`          | Multipart CSV upload; `?snapshot=true` (default) does full snapshot |
| POST | `/employees`         | Same for employees |
| GET  | `/runs?take=50`      | Recent sync runs |
| GET  | `/health`            | One-shot health summary for dashboards |

## Admin UI surface

| Route | Persona | View |
|---|---|---|
| `/admin/tenants`               | SystemAdmin | List + suspend/re-enable |
| `/admin/tenants/new`           | SystemAdmin | 4-step onboarding wizard (Client row + bootstrap TenantAdmin + temp password) |
| `/admin/directory`             | TenantAdmin | Landing page: health badge, source, totals, recent 10 runs, inline CSV upload |
| `/admin/directory/setup`       | TenantAdmin | Source config form; secret fields blank-by-default (paste to replace) |
| `/admin/directory/runs`        | TenantAdmin | Paginated sync run history |
| `/admin/directory/students`    | TenantAdmin | Paginated roster + search |
| `/admin/directory/employees`   | TenantAdmin | Paginated roster + search |

## CSV format

Headers are case-insensitive; column order doesn't matter; only `ExternalId` + `Name` are required.

Students:
```csv
ExternalId,Name,CardIdentifier,Gender,ContactNo,PhotoPath,Program,Class,Section,Session,Version
STD-2026-001,"Imran Ahmed",CARD-0001,Male,01700000001,/photos/imran.jpg,Polytechnic,IT-3rd,A,2025-2026,Bangla
```

Employees:
```csv
ExternalId,Name,CardIdentifier,Gender,ContactNo,PhotoPath,Designation,EmployeeType
EMP-001,"Reza Karim",STAFF-001,Male,01711111111,/photos/reza.jpg,Lecturer,TEACHER
```

Fields embedded with commas should be `"quoted"`; embed a literal `"` as `""`.

## Idempotency

The writer hashes the normalized payload (SHA-256 hex over pipe-delimited fields) and stores it on the row as `SourceHash`. Subsequent syncs of unchanged rows: same hash → no-op. Changed rows: hash differs → `UPDATE`. New rows: `INSERT`. With `IsFullSnapshot=true`, rows missing from the delta become `IsActive=false` (soft delete; never hard delete — orders / wallets reference these rows).

**The hash formula is part of the contract.** Changing the order or normalization of the inputs would mark every row as `updated` on the next sync. Treat it like a schema column.

## Circuit breaker + alerts (Phase 6)

| State | Trigger | Effect |
|---|---|---|
| `Healthy` | Successful sync | Background worker dispatches normally |
| `Failing` | `ConsecutiveFailures` ≥ `MaxFailuresBeforeAlert` (default 3) | One alert sent to `Directory.AlertEmail` / `Directory.AlertPhone`; worker keeps trying every interval |
| `Disabled` | Tenant admin manually set | Worker skips this tenant entirely (no sync attempts) |

Recovery: a single successful sync resets `ConsecutiveFailures` to 0 and `HealthStatus` to `"Healthy"`.

## When to use which source

| Source | Use when | Trade-off |
|---|---|---|
| `Database` | School DB is in the same datacenter and exposes safe read-only views | Lowest latency / highest fidelity; tenant must trust storing a connection string |
| `Api` | School portal exposes a directory endpoint with HMAC auth | Cleanest separation; depends on portal team honouring contract + pagination |
| `Manual` | Small institute, no SIS at all, or proof-of-concept | Stale until next CSV upload; one-time data entry burden |
| `Disabled` | Off-boarded tenant, audit/forensic period | No sync attempts; existing rows preserved |

## Verifying it works (local)

1. Boot the app (`Migration.ApplyOnStartup=true` against a fresh canteen-saas DB).
2. Log in as `sysadmin` (Change@123 default) → `/admin/tenants/new` → create a tenant.
3. Log in as the new TenantAdmin → `/admin/directory/setup`.
4. Either pick `Database` and paste a connection string to the school DB, or pick `Manual` and upload `students.csv`.
5. Hit `Sync now`. Watch `/admin/directory` go green and the totals tick up.
6. Hit `/operator/dashboard` and scan a card — it should resolve from the local `Students` table without any school-DB round-trip.

## API source — what the portal needs to implement

Two endpoints with HMAC auth (`X-App-Key` + `X-App-Timestamp` + `X-App-Signature`), signature = base64-HMAC-SHA256( secret, `"{key}\n{ts}\n{verb}\n{path}\n{bodyHashHex}"` ). Body hash = lowercase-hex SHA-256 of body bytes (empty string for GET).

```
GET {base}/students?page=N&pageSize=M
GET {base}/employees?page=N&pageSize=M

Response shape:
{
  "items":      [ { "externalId": "...", "name": "...", ... } ],
  "totalPages": 12,
  "totalCount": 5837
}
```

Item fields are documented in `ApiDirectorySource.ApiStudent / ApiEmployee`.
