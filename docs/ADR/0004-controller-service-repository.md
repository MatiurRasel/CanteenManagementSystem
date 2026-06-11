# 0004. Strict layering — Controllers → Services → IRepository → DbContext

- **Date**: 2026-06-04
- **Status**: Accepted

## Context

The codebase started as a thin port of the cc24 school-portal architecture
where the layering was *intended* to be:

```
Controller  →  Service interface  →  Concrete service  →  IRepository<T>  →  DbContext
```

Over time, "just inject `IAppDbContext` here, it's only one query" creep set
in. As of the audit on 2026-06-04, **20 of the 36 controllers** carry a
direct `IAppDbContext` dependency, and several application services skip
`IRepository<T>` and go straight to `_db.Set<T>()`. The result:

- Controllers are not testable without a real EF Core in-memory DB.
- Business logic leaks into controllers (one well-known case mixes
  `SaveChangesAsync` with `HttpContext.SignInAsync` in the same method).
- Multi-tenant query filters are bypassed in places where a developer
  needed `IgnoreQueryFilters()` and forgot to gate the access by a service.
- A future swap to DB-per-tenant (see [ADR 0001](0001-multi-tenancy.md))
  would require touching every controller because each one talks to a
  fixed `IAppDbContext` instead of an abstraction.

## Decision

The layering rule is **non-negotiable** going forward:

1. **Controllers MUST NOT inject `IAppDbContext`, `ApplicationDbContext`,
   `DbContext`, or any `DbSet<T>`.** Their only data-access dependencies
   are application-layer service interfaces (e.g. `IMfaService`,
   `IAuthService`, `IPaymentOrchestrator`) or, for write paths, the
   CQRS `IDispatcher`.
2. **Services use `IRepository<T>` via `IUnitOfWork.Repository<T>()`** for
   reads and writes; **`IUnitOfWork.SaveChangesAsync()`** for persistence.
3. **`IRepository<T>` is the only consumer of `IAppDbContext`.** The
   default `GenericRepository<T>` lives in `Infrastructure.Persistence`
   and is the only file allowed to call `_dbContext.Set<T>()` for
   tenant-scoped reads/writes.
4. **CQRS handlers may use `IAppDbContext` directly** as an exception:
   they live inside `Application/<Feature>/Commands` and `Queries`, and
   the transaction-behavior pipeline already wraps them. Adding an
   `IRepository<T>` indirection inside a handler adds friction without
   value — the handler is already in the right layer.

## Rationale

- **Testability**: A controller that depends on `IMfaService` can be
  unit-tested with a fake `IMfaService`. A controller that depends on
  `IAppDbContext` requires a real EF Core in-memory database, sample
  data, and the whole model-builder pipeline.
- **Refactor insulation**: Swapping `IAppDbContext` for a partitioned
  DbContext (DB-per-tenant) becomes a change to `GenericRepository<T>`,
  not a change to every controller.
- **Discoverability of business logic**: When a feature lives in a
  service class, future maintainers grep for the feature name and find
  the rules in one place — not scattered across an MVC action.
- **Tenant safety**: Repositories enforce the global query filter.
  Controllers that go around the filter via `IgnoreQueryFilters()` are
  a tenant-isolation risk; the cross-tenant write safeguard in
  `ApplicationDbContext.SaveChangesAsync` adds defence in depth but the
  better defence is "no controller knows how to call that method."
- **Aligns with cc24 heritage**: this repo's lineage is the cc24 school
  portal, which followed the strict pattern. Returning to it preserves
  the institutional reading of "where does X live?"

## Consequences

### We accept

- **A migration backlog**. As of this ADR, 20 controllers and at least
  6 services need refactoring. The work is tracked in
  `docs/GapsRoadmap.md` under "Layering migration." New code MUST follow
  the rule from this date forward; legacy code is migrated opportunistically
  when the surrounding controller is touched.
- **More files per feature**. A controller that previously was 200 lines
  with inline EF queries becomes a 50-line controller + a 150-line
  service + a 20-line interface. We accept this cost — the indirection
  pays off the moment the feature gains its second consumer (a CLI
  task, an API endpoint, a unit test).
- **`IUnitOfWork` becomes the central seam**. Services injected with
  `IUnitOfWork` can call `Repository<T>()` for any `T` and never need
  the DbContext. We do NOT inject `IRepository<T>` directly because
  changes to the repository contract would ripple through every service.

### We gain

- **Architectural enforcement is grep-able**: a CI check (planned) will
  fail the build if a file under `**/Controllers/**` imports
  `Microsoft.EntityFrameworkCore` or `Platform.Application.Persistence.IAppDbContext`.
- **Each feature's "blast radius" shrinks** to its service.

## Migration policy

- **New code**: enforced immediately.
- **Existing controllers**: refactor when touched for ANY reason.
- **Audit cadence**: the controller list in
  `docs/GapsRoadmap.md > Layering migration` is reconciled at the start
  of each sprint.

## Reference implementation

The MFA flow added in 2026-06-04 is the canonical example:

- `AccountController` → `IMfaService` (no DbContext)
- `IMfaService` → `IUnitOfWork.Repository<User>()` + `IMfaRecoveryCodeService`
- `IMfaRecoveryCodeService` → `IUnitOfWork.Repository<MfaRecoveryCode>()`
- `IRepository<T>` → `IAppDbContext`

Compare against the pre-refactor commit to see the difference.

## Repository contract (Batch 1, 2026-06-04)

The `IRepository<T>` seam was enriched on 2026-06-04 to match the cc24 vocabulary
that services actually need. The split is now:

```csharp
public interface IReadOnlyRepository<T> where T : class
{
    IQueryable<T> Query();                                                // tracked
    IQueryable<T> NoTrackingQuery();                                      // AsNoTracking
    Task<T?>  GetByIdAsync(object key, CancellationToken ct = default);
    Task<T?>  GetByIdAsync(object[] keys, CancellationToken ct = default); // composite key
    Task<bool> AnyAsync(Expression<Func<T,bool>> predicate, CancellationToken ct = default);
    Task<int>  CountAsync(CancellationToken ct = default);
    Task<int>  CountAsync(Expression<Func<T,bool>> predicate, CancellationToken ct = default);
    Task<T?>   FirstOrDefaultAsync(Expression<Func<T,bool>> predicate, CancellationToken ct = default);
    Task<T?>   SingleOrDefaultAsync(Expression<Func<T,bool>> predicate, CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<T>> ListAsync(Expression<Func<T,bool>> predicate, CancellationToken ct = default);
    // + legacy GetAllAsync / FindAsync kept for backward compatibility
}

public interface IRepository<T> : IReadOnlyRepository<T> where T : class
{
    Task AddAsync(T entity, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);
    void Update(T entity);
    void UpdateRange(IEnumerable<T> entities);
    void Remove(T entity);
    void RemoveRange(IEnumerable<T> entities);
}
```

### Choosing between IRepository and IReadOnlyRepository

- **Use `IReadOnlyRepository<T>`** in services that NEVER mutate that entity
  type — reporting, validation, lookup. Makes intent obvious + prevents a
  refactor accidentally introducing a write.
- **Use `IRepository<T>`** when the service participates in a transaction
  (add / update / remove). Combine with `IUnitOfWork.SaveChangesAsync()`.

### Choosing between direct `IRepository<T>` and `IUnitOfWork.Repository<T>()`

- **Direct injection**: cleaner when you only touch one or two entity types.
- **`IUnitOfWork`**: required when one operation spans multiple entities and
  must commit atomically. The UoW caches one repo per type per request so
  repeated `Repository<X>()` calls return the same instance + share the same
  DbContext.

## Migration roadmap (cc24-style)

Tracked as a 3-batch migration:

### Batch 1 — Foundation (✅ shipped 2026-06-04)
Enrich the seam. Done above.

### Batch 2 — Services (queued)
Refactor the 7 services that still take `IAppDbContext` directly:
- `AuthService` (Platform.Infrastructure.Auth)
- `WalletService` (CanteenManagementSystem.Infrastructure.Wallets)
- `InventoryService`
- `AuditTrail` (Platform.Infrastructure.Audit)
- `TenantSettingsService`
- `ReportingService`
- (`UserDirectory` already migrated)

Each service swaps `IAppDbContext _db` → `IUnitOfWork _uow` (or per-entity
`IRepository<T>`) + uses `IUnitOfWork.SaveChangesAsync()`.

### Batch 3 — Controllers (queued)
Refactor the 19 controllers that still inject `IAppDbContext`. Each one
extracts a feature service and removes its DbContext dependency. Listed in
[GapsRoadmap.md > Layering migration backlog](../GapsRoadmap.md).
