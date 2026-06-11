# 0002. Auth multiplexer: Cookie + JWT + ApiKey in one pipeline

- **Date**: 2026-06-04
- **Status**: Accepted

## Context

The platform serves three audiences that authenticate differently:

| Audience | Credential | Lifetime |
|---|---|---|
| Web browser (operator, admin, parent) | Username + password → cookie | Sliding 8h |
| Mobile / SPA | OAuth-style refresh + bearer JWT (HS256) | 15 min access + 14d refresh |
| Partner integrators (webhooks, ETL, SIS bridge) | HMAC-signed `ApiKey` | Permanent until rotated |

Most ASP.NET Core samples pick one and route the others through a separate
host. That doubles deployment complexity, doubles the surface area to harden,
and forces the same authorisation policy (e.g. `[Authorize(Roles="Admin")]`)
to be expressed twice.

## Decision

Register all three schemes side-by-side as **AuthenticationSchemes** on every
authorisation policy. The same `[Authorize(Policy=...)]` attribute accepts a
caller authenticated by any one of:

- `PlatformCookie` — default browser cookie scheme.
- `JwtBearer` — `Authorization: Bearer eyJ...`
- `ApiKey` — `X-Api-Key: ...` + `X-Api-Timestamp: ...` + `X-Api-Signature: ...`
  (HMAC-SHA256 of the canonical request, prevents replay via the timestamp).

## Rationale

- **One controller, three callers**. The webhook ingestion endpoint, the
  mobile app, and the operator browser all hit the same `MeController.Get`,
  the same `OrdersController.Place`. Without the multiplexer, every endpoint
  has to be duplicated or shim-routed.
- **Authorisation logic stays in one place**. Policies like `RequireAuditor`
  or `RequireTenantAdmin` are defined once. Adding a new policy doesn't
  require remembering to register it on three pipelines.
- **Failure mode is "try the next scheme"**. The custom auth handler runs
  each registered scheme in order; the first one that succeeds wins, the
  others are skipped. A 401 means *no* scheme succeeded.

## Consequences

### We accept

- **`AddAuthentication(...).AddCookie(...).AddJwtBearer(...).AddScheme<>(...)`
  is more wiring at startup**. We pay this once at composition root; we save
  it forever in controllers.
- **Logging requires attention**. The handler logs which scheme succeeded so
  that audit-trail entries show "user U via Cookie" vs "user U via Bearer"
  — necessary for forensic analysis of token leaks.
- **HMAC ApiKey signing requires a small SDK** for partners. We ship one
  documented helper per language in `docs/API.md`.

### We gain

- **Mobile parity**: the mobile app uses the same authorisation surface as
  the browser. No "mobile-only" subset of endpoints.
- **Partner self-service**: a SIS or LMS vendor can integrate without
  cookies, without a browser, without a customer-support ticket.

## Alternatives considered

- **One scheme + ALC-style endpoint policies** (rejected): would require
  cookies for browsers AND token issuance for everyone else — a token issuer
  is itself one of the most attacked surfaces of any platform; we'd take the
  risk without the benefit.
- **OAuth provider** (rejected for now): we have ~6 partner integrations
  expected; an OAuth provider is overkill until we cross ~50. We can adopt
  one later by mapping its tokens to a `JwtBearer` scheme + adding it as a
  4th scheme in the multiplexer — non-breaking.

## How this is enforced in code

- **Registration**: `AddPlatformAuth(IConfiguration)` in
  `Platform.Presentation.Auth.AuthExtensions` registers all three schemes
  and the composite `AnyAuthenticatedAuthorizationHandler`.
- **Default**: `[Authorize]` without a scheme falls through to the composite
  handler, which tries Cookie → Bearer → ApiKey in order.
- **Audit**: the resolved `Principal` carries a `auth-scheme` claim so
  downstream audit rows record HOW the caller authenticated.
- **Rate-limit**: the `/Account/Login` action carries a separate per-IP
  policy (`auth-login`, 5/min) — token endpoints have their own
  `auth-token` policy.
