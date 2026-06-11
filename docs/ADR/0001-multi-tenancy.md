# 0001. Multi-tenancy: shared DB with explicit ClientId

- **Date**: 2026-06-04
- **Status**: Accepted

## Context

The platform needs to host many tenant organisations (schools, hospitals,
co-working spaces — anywhere a canteen exists) on a single deployment, with:

1. Strict data isolation per tenant.
2. Low per-tenant cost so pricing can be free / freemium.
3. A migration path to dedicated infrastructure for enterprise tenants.
4. Cross-tenant operations (SystemAdmin reporting, support impersonation,
   directory-sync orchestration) without rebuilding the world.

There are three industry-standard patterns:

| Pattern | Isolation | Cost | Cross-tenant ease |
|---|---|---|---|
| Shared DB, shared schema, ClientId column | Logical | Lowest | Easiest (one connection) |
| Shared DB, schema-per-tenant | Schema | Medium | Medium (search_path / dynamic schema) |
| DB-per-tenant | Strongest | Highest | Hardest (connection-string routing) |

## Decision

We use **Shared DB + ClientId column + EF global query filter + a tenant
context middleware** as the default, with an explicit `Client.IsolationMode`
flag that allows individual tenants to migrate to DB-per-tenant in the future
without rewriting domain code.

## Rationale

- **Cost**: Onboarding a new tenant is a row insert, not an infra deploy. We
  can serve 100 small tenants for the price of one VPS.
- **Cross-tenant ops are first-class**: support impersonation, system-admin
  dashboards, directory sync orchestration all run inside one DbContext.
- **Domain code is tenancy-agnostic**: developers write `Order` queries with
  no `WHERE ClientId = ?` — the EF filter is set up centrally on
  `ITenantOwned` entities, and the *only* place tenancy is read is the
  middleware. Switching to DB-per-tenant later is a connection-routing change,
  not a query rewrite.
- **Migration path baked in**: `Client.IsolationMode` is part of the schema
  from day one. When a tenant graduates to its own DB, the system-admin
  flips the flag, the new infra is provisioned, and the connection-resolver
  layer picks up the per-tenant connection string. Domain code is untouched.

## Consequences

### We accept

- **Shared blast radius**: a bug in a global query filter would expose
  cross-tenant data. We mitigate with:
  - `ITenantOwned` is a marker interface so the filter is applied uniformly.
  - The DbContext has a `SaveChangesAsync` safeguard that rejects writes
    where `entity.ClientId != currentTenant.ClientId` unless the caller is
    a SystemAdmin acting on a specific impersonated tenant.
  - Integration tests assert that a non-admin cannot read another tenant's
    rows via any controller endpoint.
- **Larger shared tables**: queries that touch e.g. `Orders` for one tenant
  pay a B-tree-seek penalty proportional to all tenants. We add a composite
  index `(ClientId, ...)` on the heaviest tables — Orders, Audit, Wallet.
- **DB-level features that are per-tenant (e.g. row-level security) are
  unused** — we enforce in the application layer. Postgres RLS / SQL Server
  RLS could harden this in a future ADR.

### We gain

- One backup, one restore, one DR plan.
- One migration pipeline.
- Cross-tenant reports are a where clause, not a fan-out fetch.
- Free-tier pricing is feasible.

## Alternatives considered

- **Schema-per-tenant** (rejected): the EF migration story across N schemas
  is painful, and we get little extra isolation over a column filter for
  the cost of much harder cross-tenant queries.
- **DB-per-tenant from day one** (rejected): we cannot price a free tier if
  every signup provisions a new database. Reserved as the upgrade path.

## How this is enforced in code

- **Marker**: `Platform.Domain.Common.ITenantOwned`
- **Filter**: applied in `ApplicationDbContext.OnModelCreating` via
  `modelBuilder.Entity<T>().HasQueryFilter(...)` for every `ITenantOwned` type
- **Context**: `Platform.Application.Abstractions.Tenancy.ITenantContext`
  (resolved per-request by `TenantContextMiddleware`)
- **Safeguard**: `ApplicationDbContext.SaveChangesAsync` calls
  `EnforceTenantWriteIsolation()` which throws `TenantIsolationException` if
  any tracked entity belongs to a tenant other than the current one (with
  a SystemAdmin escape hatch when impersonating).
- **Flag**: `Client.IsolationMode` is `IsolationMode.Shared` by default; a
  future PR may set `IsolationMode.Dedicated` and route the DbContext via
  `ITenantConnectionResolver`.
