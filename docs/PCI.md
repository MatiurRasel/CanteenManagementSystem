# PCI scoping

> The canteen-saas platform is **out-of-scope for PCI DSS** by design. This
> document records the reasoning, the architectural controls that keep it
> out-of-scope, and the operational red-flags that would pull it back in.

## Why we're out-of-scope

PCI DSS applies to systems that **store, process, or transmit** cardholder
data (PAN, CVV, full magnetic-stripe data, PIN). The platform is structured so
none of these flows ever cross our process boundary:

* All payments are tokenised by the gateway (bKash, Nagad, SSLCommerz, Stripe).
  The user is **redirected** to the gateway's hosted-checkout page; the
  card details are entered into the gateway's UI, not ours.
* Our `PaymentTransaction` row stores `TransactionRef`, `GatewayPaymentId`,
  `Amount`, `Method`, and `Status` — **no PAN, no CVV, no expiry, no
  cardholder name**.
* The webhook / callback handler verifies the gateway's signature and reads
  only the tokenised reference back. We never see card data on the inbound
  payload.
* The wallet ledger records `EntryType = Recharge` with the gateway's
  `TransactionRef` as the idempotency key — again, no card data.

This puts the platform in the **SAQ A** posture (e-commerce merchants who
outsource all cardholder-data functions to PCI-DSS-validated third parties).

## Architectural controls

| Control | Where |
|---|---|
| No card data persisted | Domain entities reviewed; `PaymentTransaction` schema fixed |
| Tokenised redirect-to-gateway | `IPaymentGateway.InitiateAsync` returns a `RedirectUrl`; controller `302`s the user |
| Signed callback verification | `IPaymentGateway.VerifyCallbackAsync` checks per-gateway HMAC / RSA signature |
| TLS-only transport | `app.UseHttpsRedirection()` · HSTS header from `SecurityHeadersMiddleware` |
| Tenant secrets at rest | DataProtection-encrypted on `TenantSetting` rows where `IsSecret=true` |
| Audit on every payment | `IAuditTrail.RecordAsync("Payment.Started" / "Payment.Succeeded" / "Payment.Failed")` |
| Rate limit on payment endpoints | Default global `200 req/30s/IP`; tighten via `[EnableRateLimiting("…")]` on the action when bot abuse appears |
| Egress whitelisted by gateway | Outbound HTTP only to the configured gateway base URLs |

## Red-flags that would pull us back into scope

If any of the following ship into the product, re-do the scoping with a QSA:

* Persisting any card field on our side (even masked PAN).
* In-line / iframe-based card input (eg PCI DSS shifts to SAQ A-EP / D).
* Forwarding a user's card form POST through our controllers (proxy mode).
* Logging the raw callback payload from a gateway that includes card data
  (the current orchestrator logs the verified shape only — keep it that way).
* Building a "saved cards" feature in-platform — this requires PCI-validated
  vaulting; use the gateway's customer-token-vault feature instead.

## Operational checklist

* ✅ TLS certificates renewed automatically (cert-manager / Let's Encrypt /
  ACM, depending on deployment).
* ✅ ASP.NET Core DataProtection keys persisted in a mounted volume
  (`/data/dp-keys` in Helm + compose; in K8s the PVC carries the keyring).
* ✅ CI scans (Dependabot + CodeQL) for vulnerable transitive libraries.
* ✅ MFA required for any role that can view `AuditEntry` / re-issue tokens.
* 🟡 Penetration test (annual) — recommended before public sign-up.
* 🟡 Tabletop incident-response review (annual) — covered loosely in
  [Runbook.md](Runbook.md) §Outages; promote to a formal IR plan once first
  paying tenant onboards.

## References

* PCI Security Standards Council — SAQ A (v4.0).
* ADR 0002 — auth multiplexer (token-based partner integration).
* ADR 0003 — OSS charter (no proprietary lock-in for crypto / cert tooling).
