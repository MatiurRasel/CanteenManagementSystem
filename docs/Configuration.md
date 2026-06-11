# Configuration

## The DB → appsettings → default chain

Every configurable value is read through `ITenantSettings`. The lookup walks
three sources, returning the first non-null value:

| Priority | Source | When to use |
|---|---|---|
| 1 | `CanteenTenantSettings` row (key/value) | Production. Tenants edit via admin UI. |
| 2 | `appsettings.json` (key with `:` instead of `.`) | Per-environment defaults. |
| 3 | Code-level fallback (`defaultValue` parameter) | Safety net. |

### Example

```csharp
var rate    = await tenantSettings.GetDecimalAsync("VAT.Rate", 0m);
var fromEmail = await tenantSettings.GetAsync("Notifications.Email.From", "no-reply@canteen.local");
```

Storage:

* DB row: `CanteenTenantSettings` `Key="VAT.Rate"`, `Value="0.075"`.
* `appsettings.json`: `"VAT": { "Rate": "0.075" }`.

The DB row wins. Bumping the row also invalidates the cache (see
[Caching.md](Caching.md)).

## Editing settings safely

1. Use `ITenantSettings.SetAsync` from a controller or admin action.
2. The service writes the row, then invalidates the
   `CacheKeys.TenantSettingsTag` tag so every node refreshes on its next read.
3. Sensitive secrets (API keys, passwords) set `isSecret=true`. The value is
   stored in plaintext today — wire `IDataProtector` to encrypt at rest
   before going to production.

## Required keys

| Key | Used by |
|---|---|
| `Payments.Bkash.AppKey` / `.AppSecret` / `.Username` / `.Password` / `.BaseUrl` | bKash gateway |
| `Payments.Nagad.MerchantId` / `.PublicKey` / `.PrivateKey` / `.BaseUrl` | Nagad gateway |
| `Payments.SslCommerz.StoreId` / `.StorePassword` / `.BaseUrl` | SSLCommerz gateway |
| `Payments.Stripe.SecretKey` / `.WebhookSecret` | Stripe gateway |
| `Payments.CallbackBaseUrl` | Override the auto-detected public base URL for webhooks |
| `Notifications.Sms.Twilio.AccountSid` / `.AuthToken` / `.From` | Twilio SMS |
| `Notifications.WhatsApp.Twilio.AccountSid` / `.AuthToken` / `.From` | Twilio WhatsApp |
| `Notifications.Email.Host` / `.Port` / `.UseSsl` / `.Username` / `.Password` / `.From` | SMTP email |
| `Notifications.Template.{key}.subject` / `.body` / `.channel` | Per-template overrides |
| `VAT.Rate` / `VAT.Bin` | Receipt VAT line |
| `Tenant.Name` / `Tenant.Address` | Receipt header overrides |
| `Receipt.VerifyBaseUrl` | QR target base URL on receipts |
| `Observability.OtlpEndpoint` / `.ServiceName` / `.UseConsoleExporter` | OpenTelemetry |
