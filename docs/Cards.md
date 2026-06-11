# NFC Card Lifecycle

## Entities

| Type | Purpose |
|---|---|
| `NfcCard` | One row per physical card. Status: Issued / Active / Blocked / Reassigned / Retired / Lost. |
| `CardEvent` | Immutable audit trail row per lifecycle change. |

## Commands

| Command | Action | Cache touches |
|---|---|---|
| `IssueCardCommand` | Insert NfcCard + CardEvent(Issued). | Invalidate `card:uid:{uid}` |
| `BlockCardCommand` | Status = Blocked, stamp BlockedAtUtc, CardEvent(Blocked). | Invalidate `card:uid:{old}` |
| `ReassignCardCommand` | Swap CardUid, Status = Active, CardEvent(Reassigned). | Invalidate both old + new UID keys |

## API

```
GET    /api/v1/cards/by-uid/{cardUid}    (cached 5 min)
POST   /api/v1/cards/issue
POST   /api/v1/cards/block
POST   /api/v1/cards/reassign
GET    /api/v1/cards/{cardId}/history
```

All commands flow through the dispatcher pipeline (Validation → Logging →
Transaction → Handler) so any failure rolls back atomically.

## Why cache by UID

The NFC reader produces a UID on every tap. The hot path
`card lookup -> user resolve -> wallet check` runs in milliseconds when the
card lookup is in Redis. Cache TTL of 5 minutes is short enough that a block
takes effect within a single shift change even without explicit invalidation
(which we do anyway).
