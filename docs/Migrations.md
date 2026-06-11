# Migrations & Seed

This doc covers two independent runtime concerns and how to operate them:

1. **Migration** — applying the EF Core schema to your DB
2. **Seed** — populating reference rows (permissions, roles, default tenant, etc.)

Each is controlled by its own `appsettings.json` key (`Migration` / `Seed`) and can be toggled independently per environment.

---

## Two independent toggles

```jsonc
"Migration": {
  // false by default in this repo — flip ON only when DefaultConnection points
  // at a CANTEEN-OWNED DB. Never enable while DefaultConnection points at the
  // legacy school DB (ccpc_c217) — apply the --idempotent SQL script instead.
  "ApplyOnStartup":    false,
  "RetryCount":        3,
  "RetryDelaySeconds": 5
},
"Seed": {
  "RunOnStartup":        true,
  "DefaultUserPassword": ""    // override "Change@123" via user-secrets
}
```

| Environment | `Migration.ApplyOnStartup` | `Seed.RunOnStartup` |
|---|---|---|
| Local dev (fresh canteen-saas DB) | `true` | `true` |
| Local dev (still pointed at ccpc_c217) | **`false`** — apply SQL manually | `true` |
| Staging / QA | `true` | `true` |
| Production | **`false`** — release pipeline runs migrations | `true` |
| CI build | `false` | `false` |

Override via env vars (containers / pipelines):
```
Migration__ApplyOnStartup=false
Seed__RunOnStartup=true
```

---

## Current migration: `InitialCanteenSaasSchema`

Generated 2026-06-02. Creates **26 tables, 0 views**.

| Group | Tables |
|---|---|
| Identity (7) | `AppUsers`, `AppRoles`, `AppPermissions`, `AppUserRoles`, `AppRolePermissions`, `AppRefreshTokens`, `AppApiKeys` |
| Tenancy (1) | `Clients` |
| Directory (3) | `Students`, `Employees`, `DirectorySyncRuns` |
| Canteen (9) | `CanteenFoodItems`, `CanteenDailyMenus`, `CanteenWeeklyMenuTemplates`, `CanteenOrders`, `CanteenOrderItems`, `CanteenUserBalances`, `CanteenWalletLedger`, `CanteenNfcCards`, `CanteenCardEvents` |
| Payments (3) | `CanteenPaymentGatewayConfigs`, `CanteenGatewayChannels`, `CanteenPaymentTransactions` |
| Misc (3) | `CanteenTenantSettings`, `CanteenAuditEntries`, `CanteenNotificationLog` |

Notable filtered-unique indexes (multi-tenant business keys):
```sql
CREATE UNIQUE INDEX [IX_Students_ClientId_ExternalId]
  ON [Students] ([ClientId], [ExternalId]) WHERE [ClientId] IS NOT NULL;
CREATE UNIQUE INDEX [IX_Employees_ClientId_ExternalId]
  ON [Employees] ([ClientId], [ExternalId]) WHERE [ClientId] IS NOT NULL;
```

Legacy `vw_StudentInfo_Canteen` / `vw_EmployeeInfo_Canteen` are **excluded** — they belong to the source school DB.

Artifacts on disk:
- [src/CanteenManagementSystem.Infrastructure/Migrations/](../src/CanteenManagementSystem.Infrastructure/Migrations/) — EF migration code (committed)
- [migrations/InitialCanteenSaasSchema.sql](../migrations/InitialCanteenSaasSchema.sql) — 1,214-line idempotent SQL (committed for review)

---

## Applying the migration

You have three paths. **Pick one** — don't mix them.

### Path A — Fresh `canteen_saas` DB (recommended)

This is the clean-room option. Provision an empty DB; everything lands there.

```bash
# 1. Provision an empty DB on the target server
sqlcmd -S <server> -Q "CREATE DATABASE canteen_saas COLLATE Latin1_General_100_CI_AS_SC_UTF8"

# 2. Update appsettings (or env var) — point DefaultConnection at the new DB
#    "ConnectionStrings": { "DefaultConnection": "Server=...;Database=canteen_saas;..." }

# 3. Flip the toggle
#    "Migration": { "ApplyOnStartup": true }

# 4. Run the app once — boot-time seeder calls Database.MigrateAsync
dotnet run --project src/CanteenManagementSystem.Presentation
```

What you'll see in logs:
```
[INF] Database migrations applied (attempt 1/4).
[INF] Running 5 seed contributor(s).
[INF] Seeded default tenant CCPC (Id=1).
[INF] Seeded 15 permission(s).
[INF] Seeded 7 role(s) and N role-permission link(s).
[INF] Seeded 3 default user(s). Bootstrap password is in use — change immediately.
[INF] Seeded 5 gateway(s) and 8 channel(s).
[INF] Database seeding complete.
```

If you'd rather drive migrations from the CLI:
```bash
dotnet ef database update \
  --project src/CanteenManagementSystem.Infrastructure \
  --startup-project src/CanteenManagementSystem.Presentation
```

### Path B — Co-exist in `ccpc_c217` (school DB)

Use this if you want canteen tables to live in the existing school DB. The `--idempotent` SQL is guarded so school objects are untouched.

```bash
# 1. Keep DefaultConnection pointed at ccpc_c217
# 2. Keep "Migration:ApplyOnStartup" = false  (defensive default)
# 3. Apply the SQL manually:
sqlcmd -S 103.112.52.89,51433 -d ccpc_c217 -U sa -P <pass> \
  -i migrations/InitialCanteenSaasSchema.sql
# 4. Boot the app — the seeder still runs and populates reference rows
dotnet run --project src/CanteenManagementSystem.Presentation
```

The SQL is 1,214 lines but every `CREATE TABLE` is wrapped in:
```sql
IF NOT EXISTS (SELECT * FROM sys.tables WHERE [name] = 'AppUsers')
BEGIN
    CREATE TABLE [AppUsers] (...)
END;
```
so re-running is a no-op. Re-running after a partial failure is safe.

### Path C — Separate canteen DB + cross-DB reads from school DB

For now this is just A or B + the bridge in `UserDirectory`. The two-connection-string version arrives in Phase 3.

---

## Generating a new migration (after schema changes)

```bash
# 1. Edit entities / DbContext mappings.
# 2. Add a migration.
dotnet ef migrations add <ShortDescription> \
  --project src/CanteenManagementSystem.Infrastructure \
  --startup-project src/CanteenManagementSystem.Presentation \
  --output-dir Migrations

# 3. Review the generated Up()/Down() in src/.../Migrations/<timestamp>_<name>.cs
# 4. Regenerate the idempotent SQL for review/manual apply.
dotnet ef migrations script --idempotent \
  --project src/CanteenManagementSystem.Infrastructure \
  --startup-project src/CanteenManagementSystem.Presentation \
  --output migrations/<ShortDescription>.sql

# 5. Apply on dev (one of):
dotnet ef database update --project src/CanteenManagementSystem.Infrastructure --startup-project src/CanteenManagementSystem.Presentation
#   OR boot the app with "Migration:ApplyOnStartup": true
#   OR apply the SQL script manually
```

## Rolling back

```bash
# Undo the last UNAPPLIED migration (deletes the .cs files, before you ran update)
dotnet ef migrations remove \
  --project src/CanteenManagementSystem.Infrastructure \
  --startup-project src/CanteenManagementSystem.Presentation

# Or roll the DB back to a specific older migration
dotnet ef database update <OlderMigrationName> \
  --project src/CanteenManagementSystem.Infrastructure \
  --startup-project src/CanteenManagementSystem.Presentation
```

Never edit a committed migration file — generate a NEW migration that fixes the previous one.

---

## Seed — data lives in code

Seed content is **never** in `appsettings.json`. Each concern has a class in `Platform.Infrastructure.Seeding.Defaults` that implements `ISeedContributor`:

| File | Order | Inserts |
|---|---|---|
| `ClientsSeed.cs` | 5 | Default tenant row (`Clients` table) |
| `PermissionsSeed.cs` | 10 | All permission codes |
| `RolesSeed.cs` | 20 | Roles + role-permission links |
| `UsersSeed.cs` | 30 | Bootstrap users (sysadmin / ccpcadmin / ccpcoperator) |
| `GatewaysSeed.cs` | 40 | Default gateways + channels (cash / ssl / bkash / nagad / stripe) |
| `CanteenSeed.cs` (canteen product) | 100 | Default food items |

Each contributor's `SeedAsync` is **idempotent** (`SELECT ... WHERE businessKey = ...` before INSERT). Editing the static `Defaults` list and restarting only inserts the new rows.

### Adding a permission

```csharp
// Platform.Infrastructure/Seeding/Defaults/PermissionsSeed.cs
public static readonly IReadOnlyList<(string Code, string Display)> Defaults = new[]
{
    // existing entries…
    ("Receipt.Reprint", "Reprint a customer receipt"),   // ← add a row
};
```
Restart. The new permission is inserted; existing rows untouched.

### Adding a product seed

```csharp
public sealed class DefaultMenuSeed : ISeedContributor
{
    public int Order => 100;
    public async Task SeedAsync(CancellationToken ct) { /* INSERT FoodItem rows */ }
}

// DependencyInjection.cs
services.AddScoped<ISeedContributor, DefaultMenuSeed>();
```

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| `Microsoft.EntityFrameworkCore.Design is required` | `dotnet add src/.../Presentation package Microsoft.EntityFrameworkCore.Design` |
| Migration adds `vw_*_Canteen` as tables | Confirm the view-backed entity has `.ToView("...")` in `OnModelCreating` and **no** `[Table(...)]` attribute on the class |
| Boot fails: "Cannot find Migration table" | Means `__EFMigrationsHistory` is missing — the DB hasn't been initialised. Apply the SQL or run `dotnet ef database update` |
| Seed throws "Clients table not found" | Migration didn't run. Check `Migration.ApplyOnStartup` or apply SQL manually |
| Boot succeeds but no rows in `Clients` | `Seed.RunOnStartup` is `false`, or `ClientsSeed` already ran (rerun is no-op) |
| `__EFMigrationsHistory` already has entries from a different lineage | Drop the DB and start fresh, or migrate the history table by hand (rare) |

---

## Disabling at runtime

```json
// appsettings.Production.json — release pipeline owns migrations
{
  "Migration": { "ApplyOnStartup": false },
  "Seed":      { "RunOnStartup":   true  }
}
```
