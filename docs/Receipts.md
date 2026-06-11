# Receipts

QuestPDF (MIT, Community license valid for < $1M ARR) renders the customer
receipt + kitchen ticket.

## Endpoints

* `GET /api/v1/receipts/{orderId}/pdf` → A5 customer receipt with VAT line + QR
* `GET /api/v1/receipts/{orderId}/kitchen` → 80mm thermal kitchen ticket

## Branding

Pulled at render time from `BrandingConfiguration` + `ITenantSettings`:

| Setting | Default |
|---|---|
| `Tenant.Name` | `BrandingConfiguration.AppName` |
| `Tenant.Address` | empty |
| `VAT.Rate` | `0m` |
| `VAT.Bin` | empty |
| `Receipt.VerifyBaseUrl` | `https://example.com` |

## QR verification

The QR encodes `{Receipt.VerifyBaseUrl}/r/{OrderNumber}`. Create a thin
`ReceiptVerificationController` to render a public-readable summary page; that
controller doesn't exist yet — left as a TODO so consumer-facing UX can be
designed alongside.

## Adding more receipt types

`IReceiptService.GenerateXxxAsync` is the contract. To add e.g. an invoice
or a refund slip:

1. Add a method to `IReceiptService`.
2. Build the QuestPDF document in `QuestPdfReceiptService`.
3. Expose an endpoint in `ReceiptsController`.
