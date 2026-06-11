# Canteen Management System — Operator Runbook (A → Z)

This is the single doc to read before standing the system up. Every step is concrete; nothing is skipped.

---

## 0. Inventory — what's in the repo

```
platform/                                 reusable Clean-Architecture libs
  Platform.Domain/                        entities (Identity, Tenancy, Directory, Payments, Notifications…)
  Platform.Application/                   abstractions, CQRS, settings, seeding contracts
  Platform.Infrastructure/                in-house dispatcher, cache, auth, sync, payments, notifications
  Platform.Presentation/                  middleware (tenancy), auth schemes, seed extensions

src/
  CanteenManagementSystem.Domain/         canteen-specific entities (Menu, Orders, Wallet, Students, Employees)
  CanteenManagementSystem.Application/    handlers, validators
  CanteenManagementSystem.Infrastructure/ DbContext, EF mappings, CanteenDirectoryWriter, seed contributors
  CanteenManagementSystem.Presentation/   MVC + API entry point

migrations/InitialCanteenSaasSchema.sql   1,214-line idempotent SQL — apply with sqlcmd
docs/Migrations.md                        toggles + apply paths + EF workflow
docs/Directory.md                         directory sync architecture + REST + UI
docs/Runbook.md                           THIS FILE
```

---

## 1. Prerequisites

| Tool | Version | Purpose |
|---|---|---|
| .NET SDK | **10.0** | Build + run |
| `dotnet ef` CLI | 10.0+ | Migration generation (already in `.config/dotnet-tools.json`) |
| SQL Server | 2019+ / Azure SQL | Application DB |
| Redis | optional | Distributed L2 cache (in-memory used if absent) |
| sqlcmd | optional | Manual SQL apply path |

Restore tools after clone:
```powershell
dotnet tool restore
```

---

## 2. Choose your DB topology

Pick ONE — the SQL script handles both.

| Topology | When | Connection string `Database=` |
|---|---|---|
| **A. Fresh canteen-saas DB** (recommended) | Clean install, new tenants, multi-tenant SaaS | `canteen_saas` (new empty DB) |
| **B. Co-exist in ccpc_c217** | Single-tenant CCPC, keep school + canteen in same DB | `ccpc_c217` (existing school DB) |

### 2A. Provision a fresh canteen-saas DB
```powershell
sqlcmd -S <server> -U sa -P <pwd> -Q "CREATE DATABASE canteen_saas COLLATE Latin1_General_100_CI_AS_SC_UTF8"
```

### 2B. Stay on `ccpc_c217`
Nothing to do — already the default connection string.

---

## 3. Configure secrets (one-time)

```powershell
cd src\CanteenManagementSystem.Presentation

# Required: JWT signing key (any 48+ bytes, base64 or random text)
dotnet user-secrets set "Auth:SigningKey" "REPLACE-ME-WITH-48-OR-MORE-RANDOM-BYTES"

# Override the bootstrap user password (otherwise defaults to "Change@123")
dotnet user-secrets set "Seed:DefaultUserPassword" "PutAStrongTempPasswordHere"

# Switch the connection if using Topology A
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=...;Database=canteen_saas;User ID=sa;Password=...;TrustServerCertificate=True;Application Name=CCCMS;"
```

Production overrides — env vars (containers, CI/CD):
```
Auth__SigningKey=...
ConnectionStrings__DefaultConnection=...
ConnectionStrings__Redis=redis-host:6379
Migration__ApplyOnStartup=false
Seed__RunOnStartup=true
```

---

## 4. Apply the schema

Pick ONE.

### 4a. Auto-apply on boot (Topology A only — fresh DB)
```powershell
# In appsettings (or user-secrets):
#   "Migration": { "ApplyOnStartup": true }
dotnet run --project src\CanteenManagementSystem.Presentation
```
Watch the log:
```
[INF] Database migrations applied (attempt 1/4).
[INF] Running 6 seed contributor(s).
[INF] Seeded default tenant CCPC (Id=1).
[INF] Seeded 15 permission(s).
[INF] Seeded 7 role(s) and N role-permission link(s).
[INF] Seeded 3 default user(s).
[INF] Seeded 5 gateway(s) and 8 channel(s).
[INF] Seeded 8 default food item(s) for the canteen product.
[INF] Database seeding complete.
```

### 4b. Apply via sqlcmd (Topology B or prod)
```powershell
sqlcmd -S <server> -d <database> -U <user> -P <pwd> -i migrations\InitialCanteenSaasSchema.sql
```
The script is `--idempotent`: existing tables are skipped. Boot the app afterwards with `Migration:ApplyOnStartup=false`.

### 4c. EF CLI (dev)
```powershell
dotnet ef database update --project src\CanteenManagementSystem.Infrastructure --startup-project src\CanteenManagementSystem.Presentation
```

---

## 5. Boot the app

```powershell
dotnet run --project src\CanteenManagementSystem.Presentation
```
Open https://localhost:5001 (or whichever port appears in the log).

What boots:
- HTTP pipeline: Serilog request logging → GlobalException → HTTPS redirect → static files → output cache → localization → routing → tenant middleware → auth → endpoints.
- Hosted services: `ReadyOrderAutoCancelService`, `NotificationDispatcherService`, **`DirectorySyncBackgroundService`** (ticks every minute after a 30-second warm-up).

---

## 6. First-login walkthrough

### 6a. Sign in as System Admin
1. Browser → `/Account/Login`
2. Username: `sysadmin` &nbsp; · &nbsp; Password: `Change@123` (or whatever you set via user-secrets)
3. After successful auth, the sidebar reveals **Tenants** + **Directory** under "Administration".

> **Stuck on the change-password screen, or the new password isn't accepted on the next login?**
> The most common cause is a trailing whitespace pasted from a password manager or browser autofill. Reset the account with the bundled CLI tool — it bypasses the change-password flow and stamps a fresh BCrypt hash:
> ```bash
> dotnet run --project tools/ResetUserPassword -- \
>     --conn "<your connection string>" \
>     --user sysadmin \
>     --password "ChooseANewOne!2026"
> ```
> Sign in with the new password; `MustChangePassword` is set to `false` and any lockout is cleared. You can also run it via SQL directly — see `docs/Auth.md` §"Password reset SQL escape hatch".

### 6b. Onboard a tenant (or skip — `CCPC` is already seeded)
1. Sidebar → **Tenants** → `+ Add tenant`
2. Step 1 — Identity: code (slug), display name, contact info
3. Step 2 — Directory source: pick `Manual` / `Database` / `Api` (credentials configured later)
4. Step 3 — Bootstrap admin: name + email of the TenantAdmin
5. Step 4 — Confirm → tenant created. A one-time bootstrap password is shown — copy it once, share securely.

### 6c. Configure the tenant's directory source
1. Sign out, sign in as the new TenantAdmin (their email + the one-time password)
2. Sidebar → **Directory** → `Setup`
3. Choose Source:
   - **Manual**: ignore credentials, upload CSV later
   - **Database**: paste the school DB connection string + override the view names if non-standard
   - **API**: paste the school portal base URL + App Key + HMAC secret
4. Set sync interval (default 30 min) and (optional) alert email / phone
5. `Save`

### 6d. First sync
- `Directory` → `Sync now` → wait for the green badge.
- Or upload CSVs:
  ```csv
  ExternalId,Name,CardIdentifier,Gender,ContactNo,Program,Section,Session
  STD-2026-001,"Imran Ahmed",CARD-0001,Male,01700000001,Polytechnic,A,2025-2026
  ```
- Watch the status card flip to **Healthy** and `Students` count tick up.

### 6e. Verify the counter
- Sidebar → **Verification** (counter screen)
- Type or scan `STD-2026-001` (or whatever ExternalId you uploaded)
- The screen returns name + photo placeholder + balance — all from the local `Students` table.

---

## 7. Day-to-day operations

| Task | Where |
|---|---|
| Suspend / re-enable a tenant | `/admin/tenants` → toggle button |
| Change directory source | `/admin/directory/setup` |
| Force a sync | `/admin/directory` → "Sync now" |
| Inspect recent syncs | `/admin/directory/runs` |
| Search students / employees | `/admin/directory/students`, `/employees` |
| Reset failure alert | One successful sync resets `Directory.HealthStatus` to `Healthy` automatically |
| Disable a tenant's sync | `/admin/directory/setup` → Source = `Disabled` |

### Counter (operator)
| Task | Where |
|---|---|
| Verify card / ID | `/Verification` (default route) |
| Daily menu management | `/Menu/Manage` |
| Operator dashboard | `/Operator/Dashboard` |
| Public display board | `/Display/Menu` |

### Service-to-service integration (API key)
Mint via `POST /api/v1/admin/apikeys` (TODO: surface in admin UI), call any endpoint with:
```
X-App-Key:       live_xxx
X-App-Timestamp: <unix-utc-seconds>
X-App-Signature: <base64 HMAC-SHA256(secret, "{key}\n{ts}\n{verb}\n{path}\n{bodyHashHex}")>
```

---

## 8. Health + observability

| Signal | How to check |
|---|---|
| App alive | `GET /` returns the verification screen |
| Directory health (per tenant) | `GET /api/v1/admin/directory/health` (Bearer/cookie) |
| Recent sync runs | `GET /api/v1/admin/directory/runs?take=50` |
| Background worker firing | Search logs for `Directory tick ran N tenant sync(s).` |
| Failed migration | `[ERR] Migration failed after N attempt(s).` — apply manually then restart |
| Auth flow | `Account/Login` → cookie `ccs.session` should be set after success |

Recommended log shipping:
- Serilog console + rolling files (already configured in [Program.cs](src/CanteenManagementSystem.Presentation/Program.cs)).
- OpenTelemetry: set `Observability:OtlpEndpoint` to your collector.

---

## 9. Common problems

| Symptom | Fix |
|---|---|
| `/admin/*` returns 302 → /Account/Login but login then fails | Wrong username/password OR `Auth:SigningKey` is empty (JWT can't validate any prior token, but cookie should still work — check log for "Account inactive" / "Locked"). |
| Sync runs return `Source=Manual.*` empty deltas | Tenant is on Manual source → upload CSVs or switch to Database/Api in Setup. |
| `Directory.HealthStatus = Failing` | Inspect `/admin/directory/runs` → expand the latest Failed row's `ErrorMessage`. Most common: bad connection string, wrong view names, portal API 5xx. |
| `EBUSY: resource busy` while editing JSON | Stop the running app (the file is open by the host). |
| `Microsoft.EntityFrameworkCore.Design` not found | `dotnet add src\CanteenManagementSystem.Presentation package Microsoft.EntityFrameworkCore.Design` |
| Hot path slow (counter scan) | Confirm `Students` table is populated. The bridge fallback to `vw_StudentInfo_Canteen` (school DB) only fires when local row missing — verify via `/admin/directory/students`. |
| Bootstrap password forgotten | `sqlcmd ... UPDATE AppUsers SET PasswordHash = '$2a$...' WHERE UserName = 'sysadmin'` (paste a fresh BCrypt hash) OR re-run seed with a different `Seed:DefaultUserPassword`. |

---

## 10. Topology decisions made for you

These are the choices baked into the current codebase (deviate intentionally):

| Decision | Why |
|---|---|
| **Cookie + Bearer + ApiKey** schemes, all policies accept all three | Same `[Authorize(Policy = "TenantAdmin")]` works for browser admins, SPAs/mobiles, and partner integrators. |
| Cookie is the **default** scheme | Browser challenges land on `/Account/Login`. APIs still authenticate by what they carry. |
| `Migration.ApplyOnStartup` defaults **false** | Defensive — never auto-migrate the school production DB. Flip on once `DefaultConnection` points at a canteen-owned DB. |
| `Seed.RunOnStartup` defaults **true** | Seeds are idempotent (SELECT-before-INSERT) and safe to re-run. |
| `vw_*_Canteen` mapped via `.ToView()` | EF migrations DO NOT touch them — they belong to the source school DB. |
| `Students` / `Employees` are canteen-owned + filtered-unique on `(ClientId, ExternalId)` | Per-tenant business key with safe-for-legacy `WHERE ClientId IS NOT NULL` filter. |
| Sync uses `IsFullSnapshot=true` by default | Missing source rows soft-disable; never hard-delete (orders + wallets reference these rows). |
| `Directory.SourceHash` is SHA-256 of normalized fields | Skip no-op updates; sync audit only logs real changes. |
| Background worker tick = 1 min, per-tenant cadence = 30 min default | Fast response to admin-side changes, low waste. |
| Circuit breaker fires alert **once** at threshold | Single email/SMS, not a flood. |

---

## 11. What you should do RIGHT NOW

1. `dotnet tool restore`
2. Decide: fresh DB or co-exist (§2)
3. Set `Auth:SigningKey` via user-secrets (§3)
4. Apply schema (§4)
5. Boot the app (§5)
6. Sign in as `sysadmin`, walk through §6
7. Once your first counter scan succeeds against synced data, you're live.

---

## 12. What's intentionally NOT done yet

These are deferred — scoped for later iterations, not bugs:

- Native mobile apps
- AI/ML demand forecasting
- Square / Toast clones (we have bKash + Nagad + SSLCommerz + Stripe)
- Marketplace plugin store
- A separate Rent / Clinic product (scaffold ready; not built)
- Email template editor in admin UI (templates live in TenantSetting today)
- Admin UI for ApiKey provisioning (use SQL/API for now)
- Webhook receivers for portal-push directory updates (we pull on a schedule)

When you need any of these, scope them as a fresh phase — the seams are in place.
