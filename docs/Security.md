# Security overview

> Consolidated security posture for the canteen-saas platform. Companion to
> [Auth.md](Auth.md), [Caching.md](Caching.md), [Deployment.md](Deployment.md),
> [ADR 0001 multi-tenancy](ADR/0001-multi-tenancy.md), and
> [ADR 0002 auth multiplexer](ADR/0002-auth-multiplexer.md).

## At-a-glance

| Layer | Control | Status |
|---|---|---|
| **Identity** | BCrypt password hash · PBKDF2 secret hashes (Otp.NET) · cookie / JWT / API-key multiplexer | ✅ |
| **MFA** | TOTP (RFC 6238) · single-use recovery codes (PBKDF2-HMAC-SHA256, 50k iters) | ✅ |
| **Rate limit** | 5 logins/min/IP on `/Account/Login` + `/Account/verify-mfa` · global 200 req/30s | ✅ |
| **CSRF** | ASP.NET Core antiforgery on every cookie-auth POST | ✅ |
| **CSP** | Per-request nonce (24 bytes) injected via `CspNonceTagHelper` · strict mode opt-in on `/admin` + `/api` | ✅ |
| **Transport** | HSTS · X-Frame-Options · Referrer-Policy · Permissions-Policy · X-Robots-Tag | ✅ |
| **Secrets at rest** | DataProtection-encrypted secrets on `TenantSetting` (`IsSecret=true`) · keyring persisted in volume | ✅ |
| **Cross-tenant safeguard** | EF global query filter on `ITenantOwned` · `ApplicationDbContext.SaveChangesAsync` throws on `ClientId` mismatch | ✅ |
| **IP allow-list** | Optional per-tenant `Admin.IpAllowList` · single IPs + CIDR · audited denies | ✅ |
| **Audit trail** | `AuditEntry` append-only · `IAuditTrail.RecordAsync` · `AuditRetentionService` purge | ✅ |
| **Notification opt-out** | `UserNotificationPreference` · `INotificationService` respects per-user opt-out | ✅ |
| **Supply-chain** | Dependabot + CodeQL workflows · explicit `NuGetAuditSuppress` for accepted CVEs | ✅ |
| **GDPR** | Per-tenant data export ZIP (`/admin/tenancy/export`) · per-user opt-out · purge job | 🟡 deletion endpoint TBD |
| **PCI** | Card data NEVER stored on platform · payments via tokenised gateways · see [PCI.md](PCI.md) | ✅ (out-of-scope by design) |

## Auth multiplexer (ADR 0002)

Three authentication schemes register globally and all three apply to every
authorisation policy. The browser uses `PlatformCookie`; mobile / SPA uses
`Bearer` (JWT HS256); partner integrations use `ApiKey` (HMAC-SHA256 over
canonical request bytes). Tokens carry the `ClientId` claim so the tenant
context is established before any controller code runs.

## CSP nonces

`SecurityHeadersMiddleware` mints a fresh 24-byte nonce per request and stashes
it in `HttpContext.Items["csp-nonce"]`. The auto-injecting `CspNonceTagHelper`
(registered globally via `_ViewImports.cshtml`) attaches `nonce="…"` to every
`<script>` and `<style>` tag. `'unsafe-inline'` is retained as a documented
theme-compatibility fallback; set `Security:CspStrict=true` to remove it for
`/admin` and `/api` routes.

## MFA + recovery codes

TOTP enrolment lives at `/Account/mfa` (secret + otpauth:// URI shown once with
a QR generator). Login flow intercepts to `/Account/verify-mfa` and accepts
either a 6-digit TOTP **or** one of the 10 single-use recovery codes (Crockford
alphabet, formatted as `XXXXX-XXXXX`, stored as PBKDF2 hashes with 16-byte
salts). Codes are shown ONCE on issuance with copy / download / print actions.

## IP allow-list

`AdminIpAllowListMiddleware` reads the per-tenant `Admin.IpAllowList` setting on
every request whose path starts with `/admin` or `/sysadmin`. The setting is a
comma-separated mix of single IPs (IPv4 / IPv6) and CIDR ranges. Empty / missing
setting = no enforcement (so dev stays simple). Every deny emits an
`Admin.IpDenied` audit row with the source IP, path, and User-Agent.

```text
# Example tenant setting value
203.0.113.42, 203.0.113.0/24, 2001:db8::/32
```

## Cross-tenant safeguard

Every entity that implements `ITenantOwned` carries a shadow `ClientId` column
and is restricted to the current tenant via an EF Core global query filter.
`ApplicationDbContext.SaveChangesAsync` walks the change-tracker and throws
`InvalidOperationException` if any tracked write touches a row whose `ClientId`
mismatches the resolved `ITenantContext.ClientId`. This is the last-line defence
against a service-layer bug that would otherwise leak data between tenants.

## Notification opt-out

Users can opt out per (TemplateKey, Channel) via `UserNotificationPreference`
rows. `INotificationService.SendAsync(... userId)` queries the preference repo
and silently drops on a match (returns 0). Wildcards: `TemplateKey="*"` opts out
of every template on a channel; `Channel=null` opts out across every channel for
the template. Records added through the user-facing preferences UI (planned)
or directly via SQL for admin overrides.

## Audit log retention

`AuditRetentionService` background worker runs once every 24 hours, walks active
tenants, and bulk-deletes `AuditEntry` rows older than the per-tenant
`Audit.RetentionDays` setting (default 365, floor 30). Set
`Audit.RetentionEnabled=false` to pause purges for a tenant during an
investigation.

## Data subject export (GDPR)

`/admin/tenancy/export` (TenantAdmin policy) produces a ZIP archive containing
every row this tenant owns, one JSON file per entity, with a `manifest.json`
header summarising counts + export timestamp. Generated reads use
`IReadOnlyRepository<T>` so the global query filter restricts each query to the
current tenant — cross-tenant leakage is structurally impossible.

The mirror operation (irreversible tenant deletion with a hold-period) is
planned but intentionally deferred until the cascade-delete migration + recovery
UX are designed.

## Known accepted risks

| Risk | Severity | Mitigation |
|---|---|---|
| Shared-DB-with-ClientId multi-tenancy | Med | EF global filter + cross-tenant write safeguard + audited impersonation. `Client.IsolationMode = Dedicated` graduation path documented in ADR 0001. |
| CSP `'unsafe-inline'` fallback for theme compat | Low | Nonces shipped; opt-in strict mode for `/admin` + `/api` via `Security:CspStrict=true`. |
| Tenant-secret encryption at-rest is DataProtection only | Med | `ITenantSecretCipher` pluggable interface planned for KMS / HSM upgrade. |
| ImageSharp 2.1.10 has 1 moderate CVE (GHSA-rxmq-m78w-7wmc) | Low | Fix is in 3.x (dual-licensed, $1M ARR cap); conscious OSS-charter decision. Re-evaluate at $1M ARR. |
| Real payment-gateway integration is sandbox-only | Med | All four gateways (bKash / Nagad / SSLCommerz / Stripe) have full HTTP plumbing; needs vendor onboarding to flip on. |

## Reporting a vulnerability

Email `security@<your-org>` with reproducer + impact. PGP key on the public web
profile. We acknowledge within 1 business day and target a fix or mitigation
plan within 7 business days for high-severity reports.
