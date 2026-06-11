# 0003. Pure-OSS dependency charter: no revenue-capped libraries

- **Date**: 2026-06-04
- **Status**: Accepted

## Context

Modern .NET libraries increasingly ship under "split licences":

- **QuestPDF 2024+** — open-source under a license that requires a paid
  commercial fee above $1 M ARR.
- **SixLabors.ImageSharp 3.x** — Apache 2.0 OR Six Labors Split License;
  the latter applies above a revenue threshold.
- **MediatR 12+** — requires Owner pro accounts for commercial use of v13+.
- **Various OCR / charting / PDF libs** — same pattern.

These licences are reasonable for the maintainers but they put a future cost
on us proportional to our success. A SaaS that pulls one of these in becomes
hostage to a renewal negotiation the moment it grows.

The platform's pricing thesis is **free / freemium for small tenants,
paid for enterprise**. If our dependency cost grows with revenue we cannot
keep the small tier free.

## Decision

Every direct and material transitive dependency must be:

1. **OSI-approved permissive** — MIT, Apache-2.0, BSD-2/3-Clause, ISC, or
   Microsoft equivalent. **No** revenue-capped, AGPL, SSPL, BUSL, or "free
   for non-commercial" licences.
2. **Actively maintained** — last release within 18 months, or pinned with
   awareness.
3. **Replaceable** — we keep the *interface* in `Platform.Application.*` so
   swapping the implementation is a 1-file change.

## Rationale

- **Predictable cost**: our infra bill scales with usage; our software bill
  does not.
- **Aligns with our promise to free-tier tenants**.
- **Acquisition-friendly**: a buyer's due-diligence dependency audit
  surfaces zero royalty obligations.
- **Migration insurance**: if a library's licence is changed under us
  (it has happened — see ImageSharp), we have time to replace it because
  the interface seam is already in place.

## Consequences

### We accept

- **Some libraries are off-limits** that the broader ecosystem uses. We
  re-implement small bits (ESC/POS printer, CSV writer, audit-trail filter)
  rather than pull a borderline-licensed dep.
- **One CVE remains accepted at time of ADR**: `SixLabors.ImageSharp 2.1.10`
  has GHSA-rxmq-m78w-7wmc (moderate). The fix is in 3.x — dual-licensed —
  which we will not adopt. We accept this until either the 2.1.x line gets
  a patch or until the platform's revenue makes the commercial fee
  uneconomic to avoid.
- **Some features take longer to build**. PDF generation uses PdfSharpCore
  (MIT) instead of QuestPDF. Image manipulation uses ImageSharp 2.1.x
  pinned. CSV uses StringBuilder instead of a library.

### We gain

- **Truly free tier for small tenants**. Forever.
- **No surprise licence bills** when the platform scales.
- **One supply-chain audit answer**: "We are pure OSS. Here's the SBOM."

## Currently selected stack

| Need | Library | Licence |
|---|---|---|
| PDF | PdfSharpCore | MIT |
| XLSX | ClosedXML | MIT |
| QR codes | QRCoder | MIT |
| Image processing | SixLabors.ImageSharp 2.1.x (pinned) | Apache-2.0 |
| TOTP | Otp.NET | MIT |
| Bcrypt | BCrypt.Net-Next | MIT |
| Logging | Serilog | Apache-2.0 |
| ORM | EF Core | MIT |
| Telemetry | OpenTelemetry .NET | Apache-2.0 |
| Cache | StackExchange.Redis | MIT |
| Notifications transport | Twilio.NET helper (HTTP only — not the official SDK) | MIT |

The mediator pattern, query dispatcher, CQRS behaviours, ESC/POS printer,
and report renderers are all **in-house** under our MIT.

## Review cadence

This ADR is reviewed every 6 months. Updates happen via new ADRs that
either supersede this one or add exceptions for specific libraries with
explicit rationale.
