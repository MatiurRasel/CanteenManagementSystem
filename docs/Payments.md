# Payments

## High-level flow

```
[ user ] -> /api/v1/payments/start
              |
              v
       PaymentOrchestrator
              |
              +-- INSERT PaymentTransaction (status=Initiated)
              +-- IPaymentGateway.InitiateAsync (HTTP to gateway)
              +-- UPDATE PaymentTransaction (status=Pending, GatewayPaymentId)
              v
         redirect URL  --->  user pays on bKash / SSL / Stripe
              ...
              v
[ gateway ] -> /api/v1/payments/{gateway}/callback?ref={transactionRef}
              |
              v
       PaymentOrchestrator.HandleCallbackAsync
              |
              +-- IPaymentGateway.VerifyCallbackAsync (HTTP back to gateway)
              +-- UPDATE PaymentTransaction (status=Succeeded/Failed)
              +-- on Succeeded -> IWalletService.RechargeAsync (idempotent)
              +-- IAuditTrail.RecordAsync
              v
       302 back into the app with a flash toast
```

## Idempotency

`PaymentTransaction.TransactionRef` is unique and is also used as the
`IdempotencyKey` for the wallet ledger. If a gateway retries the callback (and
they do), the wallet will not double-recharge.

## Gateway impls

| Provider | File | Tenant settings |
|---|---|---|
| bKash | `Infrastructure/Payments/Bkash/BkashPaymentGateway.cs` | `Payments.Bkash.BaseUrl/AppKey/AppSecret/Username/Password` |
| SSLCommerz | `Infrastructure/Payments/SslCommerz/SslCommerzPaymentGateway.cs` | `Payments.SslCommerz.BaseUrl/StoreId/StorePassword` |
| Nagad | `Infrastructure/Payments/Nagad/NagadPaymentGateway.cs` | `Payments.Nagad.BaseUrl/MerchantId/PublicKey/PrivateKey` |
| Stripe | `Infrastructure/Payments/Stripe/StripePaymentGateway.cs` | `Payments.Stripe.SecretKey/WebhookSecret` |

Nagad requires merchant-supplied RSA keys for signing/encryption — the
gateway file is wired but the crypto step is stubbed until those land. Set
`Payments.Nagad.Enabled=true` only after wiring the keys.

## Adding a new gateway

1. Implement `IPaymentGateway` in `Infrastructure/Payments/<Name>/`.
2. Register the type in `Infrastructure.DependencyInjection`:
   ```csharp
   services.AddHttpClient<NewGateway>(c => c.Timeout = ...);
   services.AddScoped<IPaymentGateway>(sp => sp.GetRequiredService<NewGateway>());
   ```
3. Add an enum entry to `Domain/Payments/PaymentMethod.cs`.
4. Document settings in `docs/Configuration.md`.
