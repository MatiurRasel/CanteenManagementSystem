# Auth & Authorization

Three caller types, two auth schemes, one identity store.

## Caller types

| Caller | Scheme | Identity carrier |
|---|---|---|
| Operator / Admin / End-user (browser, mobile app) | `Bearer` (JWT) | `AppUsers` row + `AppRoles` |
| School portal / Partner ERP / Mobile-app backend | `ApiKey` (X-App-Key + HMAC) | `AppApiKeys` row |
| Service-to-service inside the platform | Either, depending on the call | — |

## JWT flow

```
POST /api/v1/auth/login
  { "userName": "ccpcoperator", "password": "Change@123" }

→ 200
  {
    "accessToken": "eyJ…",                  // HS256, ~15 min
    "accessTokenExpiresAtUtc": "…",
    "refreshToken": "yhFv…",                // opaque, ~7 days, ROTATED on every refresh
    "refreshTokenExpiresAtUtc": "…",
    "user": { "userId": 3, "userName": "ccpcoperator", "roles": [ "Operator" ],
              "permissions": [ "Order.Place", "Order.Deliver", "Card.Read", … ] }
  }
```

Send `Authorization: Bearer <accessToken>` on every request after login.

When the access token expires, call `POST /api/v1/auth/refresh` with the
refresh token to rotate. The old refresh token is revoked; if a revoked
refresh token is presented later (theft signal), the system revokes ALL the
user's active sessions.

## API key flow (server-to-server)

```
POST  https://canteen.example.com/api/v1/orders
X-App-Key:       live_a1b2c3…
X-App-Timestamp: 2026-06-02T11:35:12Z
X-App-Signature: HMAC-SHA256(secret, canonical) → base64
Content-Type:    application/json
{ "userId": "S-1234", "items": [ … ] }
```

Where:

```
canonical = "{X-App-Key}\n{X-App-Timestamp}\n{HTTP-VERB}\n{Path}\n{BodySha256-Hex}"
```

The handler validates the timestamp window (±5 min), rebuilds the canonical
string from the live request, runs HMAC-SHA256 with the stored secret hash,
and compares with `CryptographicOperations.FixedTimeEquals`. On success it
stamps the resolved `ClientId` onto the per-request `ITenantContext`, so
downstream EF queries scope correctly.

### Why the secret is never on the server in plaintext

Same pattern Stripe / GitHub / Twilio use: the secret is generated at
provisioning time, shown to the integrator **once**, then stored only as a
salted hash. We then HMAC the canonical string with the stored hash as the
key — i.e. the hash IS the HMAC key. Any attacker stealing the DB still
needs to brute-force the secret before they can sign requests.

## Provisioning a partner

```
POST /api/v1/admin/api-keys                (TenantAdmin)
  { "displayName": "CCPC Portal", "scopes": "orders.read,wallet.recharge" }

→ 201
  {
    "appKey":     "live_a1b2c3…",
    "secret":     "show-once-then-discard-this-locally",   // shown ONCE
    "displayName": "CCPC Portal"
  }
```

The partner stores the secret and sends `(X-App-Key, X-App-Signature,
X-App-Timestamp)` on every subsequent call. Lost it? Revoke and reissue —
the old key flips `IsActive=false`.

## Authorization

Role-based via `[Authorize(Roles = "Operator,Admin")]` and policy-based via
`[Authorize(Policy = "TenantAdmin")]`. Permission claims (`perm:Order.Place`)
are also in the JWT for fine-grained policies.

## Default seeds (from appsettings.json `Seed` section)

| Role | Permissions |
|---|---|
| SystemAdmin | * (every permission, on tenant create) |
| TenantAdmin | Wallet.Manage, Order.View, Menu.Manage, Card.Manage, Reports.View, Audit.View, Gateway.Manage |
| Operator | Wallet.View, Order.Place, Order.Deliver, Order.View, Card.Read, Verification.Run |
| Cashier | Order.Place, Order.Deliver, Wallet.View |
| Auditor | Reports.View, Audit.View, Order.View |
| Parent | Wallet.View, Order.View |
| Student | Wallet.View, Order.Place |

Default seed users (passwords are `Change@123`, `MustChangePassword=true`):

| UserName | Roles |
|---|---|
| sysadmin | SystemAdmin |
| ccpcadmin | TenantAdmin |
| ccpcoperator | Operator |
