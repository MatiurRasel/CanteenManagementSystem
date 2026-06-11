# Partner Integration API (HMAC + REST)

For service-to-service callers (school portal, parent app backend, payment
gateways) that integrate with the canteen platform without a human user.

| What | Where |
|---|---|
| Auth | HMAC over X-App-Key / X-App-Timestamp / X-App-Signature — see [docs/Auth.md](Auth.md) |
| Base URL | `https://<host>/api/v1` |
| Versioning | ASP.NET API versioning (`/api/v{1.0}/...`) — current version `1.0` |
| Tenant scoping | `X-Tenant: <ClientCode>` header (optional — falls back to subdomain / default) |
| Errors | RFC 7807 ProblemDetails JSON; HTTP status drives client logic |
| Idempotency | `Idempotency-Key` header recommended on POSTs that mutate (Orders, Recharge) |

## Issuing API keys

```bash
curl -X POST https://canteen.example.edu/api/v1/admin/apikeys \
     -H "Authorization: Bearer $SYSADMIN_JWT" \
     -H "Content-Type: application/json" \
     -d '{"displayName":"School portal","scopes":"orders.read,wallet.recharge,directory.write","allowedIps":""}'
```

Response shows the secret EXACTLY ONCE — copy it immediately.

## Core endpoints

### Auth (Bearer only)
| Verb | Path | Purpose |
|---|---|---|
| POST | `/auth/login` | Username + password → access + refresh JWT |
| POST | `/auth/refresh` | Rotate refresh token |
| POST | `/auth/logout` | Revoke a refresh token |
| POST | `/auth/change-password` | Authenticated user changes their own password |
| GET  | `/auth/me` | Echo current principal (handy for SDK self-tests) |

### Wallet
| Verb | Path | Purpose |
|---|---|---|
| GET  | `/wallets/{userId}?userType=Student` | Balance snapshot |
| POST | `/wallets/{userId}/recharge` | Internal recharge (manual) — sends SMS |

### Orders
| Verb | Path | Purpose |
|---|---|---|
| POST | `/orders` | Place an order |
| GET  | `/orders/{id}` | Detail |
| POST | `/orders/{id}/deliver` | Mark delivered (Bearer scoped to Operator) |

### Menu
| Verb | Path | Purpose |
|---|---|---|
| GET  | `/menu/today` | Public board (no auth, cached 15s) |

### Cards
| Verb | Path | Purpose |
|---|---|---|
| GET  | `/cards/{uid}` | Lookup by hardware UID |
| POST | `/cards` | Issue |
| POST | `/cards/{id}/block` | Block |

### Directory sync (partner ingest path)
| Verb | Path | Purpose |
|---|---|---|
| POST | `/admin/directory/students` | Multipart CSV upload (TenantAdmin) |
| POST | `/admin/directory/sync-now` | Trigger immediate sync from configured source |
| GET  | `/admin/directory/health` | Status summary |

### Reports
| Verb | Path | Purpose |
|---|---|---|
| GET  | `/admin/reports-catalog/{key}/download?format=Pdf&...` | Download report bytes |

Full list emerges from the OpenAPI spec at `/swagger` (dev) or
`/openapi/v1.json`.

## Calling pattern (one example)

Recharge a wallet from the school portal:

```python
import time, hmac, hashlib, base64, json, requests

KEY    = "live_xxx"
SECRET = b"..."   # bytes — keep in your vault

verb   = "POST"
path   = "/api/v1/wallets/STD-2026-001/recharge"
body   = json.dumps({"userType": "Student", "amount": 500, "source": "Portal"}, separators=(",", ":")).encode()
ts     = str(int(time.time()))
bhex   = hashlib.sha256(body).hexdigest()
canon  = f"{KEY}\n{ts}\n{verb}\n{path}\n{bhex}"
sig    = base64.b64encode(hmac.new(SECRET, canon.encode(), hashlib.sha256).digest()).decode()

r = requests.post(
    f"https://canteen.example.edu{path}",
    data=body,
    headers={
        "X-App-Key":       KEY,
        "X-App-Timestamp": ts,
        "X-App-Signature": sig,
        "X-Tenant":        "ccpc",
        "Content-Type":    "application/json"
    },
    timeout=10
)
r.raise_for_status()
```

## Webhooks (planned)

Partners can subscribe to:

* `order.placed`
* `order.delivered`
* `wallet.recharged`
* `card.lost`

Wire format: HMAC-signed POST identical to the inbound HMAC scheme (same
canonical-string algorithm, your secret signs outbound). Tracked under
`docs/GapsRoadmap.md`.

## Rate limits

| Endpoint | Limit |
|---|---|
| `/Account/Login`, `/Account/verify-mfa` | 5 req / min / IP |
| Everything else | 200 req / 30 s / IP |

Exceeded requests return `429 Too Many Requests` + `Retry-After: 60`.

## Error responses

```jsonc
HTTP/1.1 409 Conflict
Content-Type: application/problem+json

{
  "type":   "https://example.edu/errors/wallet-insufficient",
  "title":  "Insufficient balance to block.",
  "status": 409,
  "detail": "Available 12.00 (incl. emergency 0.00), requested 35.00."
}
```

Error categories:

| HTTP | Meaning |
|---|---|
| 400  | Validation (bad JSON, missing required field) |
| 401  | Auth failed (no header, bad signature, expired token) |
| 403  | Authorised but lacks permission/role |
| 404  | Resource not found |
| 409  | Business conflict (insufficient balance, dup order key) |
| 422  | Domain rule violation (lifecycle transition not allowed) |
| 429  | Rate-limited — back off + obey `Retry-After` |
| 500  | Server bug — correlation id in body's `traceId` |

`traceId` is the W3C trace context — paste into your log search to find
the matching server-side trace.
