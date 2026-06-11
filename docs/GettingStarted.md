# Getting Started — Step-by-Step Tutorial

Welcome. This guide walks you from a freshly-cloned repo to a running
**SmartCanteen** SaaS with a signed-in admin, one tenant, a handful of
users and a working counter flow. Follow it top to bottom on a clean
machine; total wall-clock is **about 30 minutes**.

The platform is **NopCommerce-style multi-tenant**: one shared database
(`SmartCanteen`) holds all tenants, separated by a `ClientId` column +
EF global query filters. There is **no bridge to any external school
portal** — every table, view and index is created and evolved by
EF Core migrations in this repo.

---

## 0. Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | **10.0.x** | `dotnet --version` should print 10.0.* |
| SQL Server | 2019+ or LocalDB | Express / Developer / Docker all fine |
| `dotnet ef` tools | 10.0+ | `dotnet tool install --global dotnet-ef` |
| Git | any | for cloning |
| (optional) Redis 7+ | | only for multi-replica scale-out |
| (optional) Docker Desktop | | only if you prefer docker-compose |

Verify:

```pwsh
dotnet --version
dotnet ef --version
sqlcmd -? > $null; if ($?) { Write-Host "sqlcmd OK" }
```

---

## 1. Clone + restore

```pwsh
git clone https://github.com/your-org/CanteenManagementSystem.git
cd CanteenManagementSystem
dotnet restore
```

You should see all 8 projects restore with one warning about
`SixLabors.ImageSharp 2.1.10` — that's an accepted moderate CVE
documented in [ADR 0003](ADR/0003-oss-charter.md). It does not block.

---

## 2. Point at YOUR SmartCanteen database

Two equally good options. Pick one.

### Option A — local SQL Server / LocalDB (simplest)

`appsettings.json` already ships with:

```
Server=localhost;Database=SmartCanteen;Trusted_Connection=True;...
```

That works out of the box if you have **SQL Server LocalDB** or a
default-instance SQL Server on `localhost`. If you need a different
host/auth, override via **user-secrets** (never commit credentials):

```pwsh
cd src/CanteenManagementSystem.Presentation
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" `
  "Server=YOUR_HOST;Database=SmartCanteen;User ID=sa;Password=YOUR_PWD;TrustServerCertificate=True;MultipleActiveResultSets=True"
```

### Option B — docker compose (all-in-one)

`docker-compose.yml` already spins up SQL Server 2022 + Redis 7 + the
app. From the repo root:

```pwsh
docker compose up --build
```

The compose file overrides the connection string to use the bundled
SQL Server with a generated password.

---

## 3. Apply migrations to create the SmartCanteen schema

The database starts empty. EF migrations create every table, index and
constraint — there is no manual SQL step.

```pwsh
dotnet ef database update `
  --project src/CanteenManagementSystem.Infrastructure `
  --startup-project src/CanteenManagementSystem.Presentation
```

What this does:

1. Connects using `ConnectionStrings:DefaultConnection`.
2. If the `SmartCanteen` database doesn't exist, **creates it**.
3. Runs every migration in order from
   `src/CanteenManagementSystem.Infrastructure/Migrations/` to build:
   - **Identity tables**: `AppUsers`, `AppRoles`, `AppPermissions`,
     `AppUserRoles`, `AppRolePermissions`, `AppRefreshTokens`,
     `AppApiKeys`, `AppMfaRecoveryCodes`.
   - **Tenancy**: `Clients` (with `IsolationMode` column).
   - **Directory**: `Students`, `Employees`, `DirectorySyncRuns`.
   - **Canteen domain**: `CanteenFoodItems`, `CanteenDailyMenus`,
     `CanteenWeeklyMenuTemplates`, `CanteenOrders`, `CanteenOrderItems`,
     `CanteenUserBalances`, `CanteenWalletLedger`.
   - **Cards**: `NfcCards`, `CardEvents`.
   - **Payments**: `PaymentGatewayConfigs`, `GatewayChannels`,
     `PaymentTransactions`.
   - **Notifications**: `NotificationLogs`.
   - **Audit**: `AuditEntries`.
   - **Tenant settings**: `TenantSettings`.
   - **Loyalty + discount**: `LoyaltyAccounts`, `LoyaltyEntries`,
     `DiscountRules`.
   - **Webhooks**: `WebhookSubscriptions`, `WebhookDeliveries`.
   - **Reporting**: `ReportSchedules`.

> **Already have a SmartCanteen DB and want to apply changes via a
> release pipeline?** Use the idempotent SQL artefact instead:
> `migrations/InitialSmartCanteen.sql` (full from zero) or the
> per-feature SQL files. Set
> `Migration:ApplyOnStartup=false` in production.

---

## 4. Set the JWT signing key

The app needs a bearer-JWT signing key. Generate one once and store it
in user-secrets:

```pwsh
cd src/CanteenManagementSystem.Presentation
dotnet user-secrets set "Auth:SigningKey" "$(([System.Convert]::ToBase64String((New-Object Security.Cryptography.RNGCryptoServiceProvider).GetBytes(48) | %{$_})))"
```

If you skip this, JWT issuance throws — the cookie login still works.

---

## 5. Run

```pwsh
dotnet run --project src/CanteenManagementSystem.Presentation
```

Watch the log:

```
[INF] Starting Canteen Management System
[INF] Now listening on: https://localhost:5001
[INF] Now listening on: http://localhost:5000
[INF] Seeded N default user(s). Bootstrap password is in use — change immediately.
```

Open <https://localhost:5001>. You'll be redirected to **`/Account/Login`**.

---

## 6. First login

The platform seeds three accounts on the first run (password is
`Change@123`, and you'll be forced to change it on first login):

| Username | Role | What they can do |
|---|---|---|
| **`sysadmin`** | SystemAdmin | Cross-tenant; manages `Clients`, can impersonate any tenant |
| **`smartadmin`** | TenantAdmin | Scoped to the default tenant; full admin for that tenant |
| **`smartoperator`** | Operator | Counter cashier — verify card, take orders, mark ready/delivered |

Sign in as **`sysadmin / Change@123`**. You'll be prompted to set a new
password. Pick something strong; you can also enrol MFA from
`/Account/mfa` once signed in.

> **MFA recovery codes**: After enrolling MFA, click "Generate /
> regenerate codes" on the MFA page. The 8 codes are shown ONCE — save
> them somewhere safe. They unlock your account if you lose your
> authenticator app.

---

## 7. Verify your tenant

`sysadmin` lands on the home page. Open the topbar tenant chip — the
default tenant **SMARTCANTEEN** was seeded by `ClientsSeed`. To rename
or add more tenants:

- Go to `/admin/tenants` (only visible to SystemAdmin).
- The bulk-import flow accepts a CSV of tenants — see
  `docs/Directory.md` for the column shape.

---

## 8. Onboard your first user batch

You said it: **Clients, Users etc are primarily created by Excel/UI per
client.** Two paths:

### 8a. Bulk CSV upload (recommended for >5 rows)

Sign in as `smartadmin` (impersonate from the topbar if you're still as
sysadmin). Go to `/admin/users/bulk-import`.

CSV columns:

```
UserName, DisplayName, Email, UserKind, RoleCode, ExternalId
alice,    Alice Khan,  a@x.com, Student, Student,  STD-001
bob,      Bob Rahman,  b@x.com, Employee, Operator, EMP-007
```

Each row that succeeds returns a temp password in the flash message —
distribute these out of band, the user is forced to change on first login.

### 8b. UI one-by-one

`/admin/users/invite` — a single-user form. Useful for adding one new
operator without preparing a CSV.

### 8c. Students / Employees rosters

`Students` and `Employees` are populated the same way — Excel/CSV bulk
upload or per-record UI under `/admin/directory`. The counter scans
their card or types their `ExternalId` to verify.

> External integration (pulling Students/Employees from a school SIS via
> API or DB view) is wired but **not used by default**. You can flip
> it on later via the per-tenant `Directory.*` settings — for now, keep
> it on the Manual source and use Excel/UI.

---

## 9. Add a tiny menu so you can test orders

As `smartadmin`:

1. Open `/admin/menu/manage` (sidebar → "Menu").
2. Add 2–3 `FoodItem` rows (name, price, category, optional photo).
3. Add a `DailyMenu` entry for today using one of the food items.

You now have one operator, three users, two food items and a daily
menu. Time to ring up a sale.

---

## 10. Test the counter flow

1. Open a **second browser session** (different profile or incognito).
2. Sign in as **`smartoperator / Change@123`** (set a new password).
3. The default landing is `/Verification` — the operator scan screen.
4. Type the `ExternalId` of one of the users you imported and press
   Enter (or scan the matching NFC card if you have a reader).
5. You should see their balance + the daily menu.
6. Tap an item, confirm — the order goes to the kitchen.
7. Switch to **`/Kitchen`** (a third tab works) and watch the order
   appear in real time. Mark it Preparing → Ready → Delivered.
8. The counter screen toasts the status changes via SignalR.
9. Check the user's balance — the order amount was deducted on delivery.

That's a full happy-path sale. Receipt PDF is at `/admin/orders/{id}/receipt`.

---

## 11. Explore the admin dashboards

While signed in as `smartadmin`:

| Path | What it shows |
|---|---|
| `/admin/dashboard` | Today's revenue, popular items, peak-hour heatmap |
| `/admin/audit` | Every important event with `Action` / `EntityType` filters |
| `/admin/reports-catalog` | All registered IReports — Daily collection, Popular items, Shift summary, Wallet distribution, Refund/void log, Audit. Download as PDF, XLSX, CSV, HTML or JSON. |
| `/admin/gateways` | bKash, Nagad, SSLCommerz, Stripe — pick sandbox/live, set keys |
| `/admin/notification-templates` | Edit SMS / email / WhatsApp templates per tenant. Test-send button! |
| `/admin/discount-rules` | "Tuesday teacher 10%", "Lunch combo Fri-only", etc. |
| `/admin/webhooks` | Subscribe partners to `order.placed`, `order.delivered`, ... events |
| `/admin/report-schedules` | Cron-style automated email of any IReport |
| `/admin/cards` | Issue / activate / block / reassign / retire NFC cards |
| `/admin/users` | User CRUD + bulk import + invite |
| `/admin/directory` | Manual source for Students/Employees if SIS sync is off |

---

## 12. What to test next

Mental checklist of things worth exercising before going to a real pilot:

- [ ] Bulk import 50 Students from CSV; verify EF query filters keep
      another tenant's data invisible.
- [ ] Recharge a wallet (`/Wallet/Recharge` API or the Parent portal at
      `/parent`). SMS will fire if you've set Twilio creds in tenant
      settings.
- [ ] Trigger a refund (`VoidOrderCommand`) on a delivered order;
      verify wallet credit + audit row + webhook delivery.
- [ ] Enable MFA on `sysadmin`, sign out, sign in, use a recovery code.
- [ ] Switch to a second app instance (Redis backplane required) and
      verify a SignalR order:placed event arrives on both nodes.
- [ ] Run `dotnet test` — should report 22 / 22 passing.
- [ ] Hit `/health/ready` — should report `Healthy` with DB + directory
      sub-systems green.
- [ ] Open `/metrics` — Prometheus exposition format with HTTP, EF Core
      and runtime metrics.

---

## 13. Where to look when things break

| Symptom | Where to look |
|---|---|
| Cannot connect to DB | `dotnet ef database update` failed → connection string |
| "DefaultClientId not seeded" | `ClientsSeed` didn't run → check `Seed:RunOnStartup` |
| Login keeps redirecting | Cookie domain mismatch; check `Auth` + you're on HTTPS |
| 401 on every endpoint | Wrong tenant context; check `client_id` claim + `Tenancy` |
| MFA login locked | Use a recovery code from `/Account/mfa` regenerate flow |
| Sluggish under load | Set `ConnectionStrings:Redis` and turn on the L2 cache |
| Multi-replica desync | Add Redis (covers L2 + SignalR backplane) |
| Migration error mid-deploy | Each migration has an idempotent SQL artefact in `migrations/` |

---

## 14. Architecture map (one-screen)

```
┌──────────────────────────────────────────────────────────────────────┐
│ Presentation (MVC + SignalR + OpenAPI + middleware)                  │
│   • AccountController, MeController, KitchenController, …             │
│   • Hubs/, Middleware/SecurityHeaders, TenantContext, RateLimiter     │
│   • Per ADR 0004: controllers depend on application services ONLY    │
├──────────────────────────────────────────────────────────────────────┤
│ Application (CQRS + abstractions)                                    │
│   • IRequest/ICommand/IQuery + IDispatcher + pipeline behaviours      │
│   • IAuthService, IMfaService, IWalletService, IPaymentOrchestrator   │
│   • IReport, IReportRenderer, IReportDispatcher                       │
├──────────────────────────────────────────────────────────────────────┤
│ Infrastructure (services + EF + 3rd-party adapters)                  │
│   • Implementations behind every abstraction in Application           │
│   • Caching (L1 + L2 Redis), Audit, Wallet, Payments, Reporting       │
│   • Repositories (IRepository<T>) — the only consumers of DbContext   │
├──────────────────────────────────────────────────────────────────────┤
│ Domain (entities + enums + invariants)                                │
│   • Pure POCOs; no EF / ASP.NET / HttpClient references               │
└──────────────────────────────────────────────────────────────────────┘
```

The cross-cutting concerns (logging, tracing, metrics, exception
handling, security headers, caching, audit, tenancy, validation, rate
limiting, real-time, background jobs) are listed in
[GapsRoadmap.md > CrossCutting concerns](GapsRoadmap.md) — each lives
in the layer that owns its dependencies.

---

## 15. Where to read next

- **[Architecture.md](Architecture.md)** — full layering + dependency rule
- **[ADR/0001-multi-tenancy.md](ADR/0001-multi-tenancy.md)** — shared-DB-with-ClientId design
- **[ADR/0004-controller-service-repository.md](ADR/0004-controller-service-repository.md)** — non-negotiable layering rule + migration backlog
- **[Auth.md](Auth.md)** — Cookie + JWT + ApiKey multiplexer + MFA
- **[API.md](API.md)** — Partner / mobile integration with HMAC signing
- **[Caching.md](Caching.md)** — L1+L2 + OutputCache behind a load balancer
- **[Deployment.md](Deployment.md)** — single-host + Kubernetes (Helm) paths
- **[Reporting.md](Reporting.md)** — how to add a new IReport
- **[Migrations.md](Migrations.md)** — when to use code-first vs SQL artefact
- **[GapsRoadmap.md](GapsRoadmap.md)** — what's done, what's missing, what's next
