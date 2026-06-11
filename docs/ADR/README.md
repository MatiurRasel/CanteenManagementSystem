# Architecture Decision Records (ADRs)

Numbered, immutable records of significant architectural decisions, why we made
them, and what we accept as a consequence. New decisions get a new number;
**we never edit a merged ADR — we supersede it.**

## Format

Each ADR follows the lightweight [MADR](https://adr.github.io/madr/) template:

```
# NNNN. Short decision title
Date: YYYY-MM-DD
Status: Proposed | Accepted | Superseded by NNNN | Deprecated

## Context        Why we are deciding this now.
## Decision       What we chose.
## Rationale      Why this over the alternatives we considered.
## Consequences   What we now accept — good and bad.
## Alternatives   Brief notes on what we rejected.
```

## Index

| # | Title | Status |
|---|---|---|
| [0001](0001-multi-tenancy.md) | Multi-tenancy: shared DB with explicit ClientId | Accepted |
| [0002](0002-auth-multiplexer.md) | Auth multiplexer: Cookie + JWT + ApiKey in one pipeline | Accepted |
| [0003](0003-oss-charter.md) | Pure-OSS dependency charter: no revenue-capped libraries | Accepted |
| [0004](0004-controller-service-repository.md) | Strict layering — Controllers → Services → IRepository → DbContext | Accepted |

## When to write an ADR

Write an ADR when the decision is:

- **Architectural** — changes how subsystems plug together (not a bug fix).
- **Costly to reverse** — would break consumers, require a migration, or
  invalidate institutional knowledge.
- **Surprising in isolation** — a future maintainer reading the code alone
  would not understand *why* we chose this path.

You do NOT need an ADR for: routine refactors, additive features that fit the
existing seams, dependency version bumps, or test-only changes.
