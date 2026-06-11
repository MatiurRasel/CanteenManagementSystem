// =============================================================================
// INbrReceiptService  (Platform.Application.Abstractions.Receipts)
// -----------------------------------------------------------------------------
// Bangladesh National Board of Revenue (NBR) Mushak 6.3 e-receipt integration.
//
// Every VAT-registered restaurant / canteen in BD must:
//   1. Compose a receipt payload conforming to NBR's e-receipt schema.
//   2. POST it to NBR's e-Mushak gateway.
//   3. Store the returned Fiscal Reference Number (FRN) + signature on the
//      order's receipt and surface it on the printed receipt.
//
// The platform exposes this seam BUT ships only a stub impl. Production
// deployments wire the real HTTP client + signing key once the merchant has
// onboarded with NBR — credentials are tenant-specific and can't be checked
// in.
//
// TENANT CONFIG (CanteenTenantSettings, IsSecret as noted)
//   Nbr.Enabled               bool   (default false)
//   Nbr.Bin                          tenant Business Identification Number
//   Nbr.MerchantToken         SECRET
//   Nbr.SigningKey            SECRET (RSA private key, PEM)
//   Nbr.GatewayBaseUrl               e.g. https://emushak.nbr.gov.bd/api
// =============================================================================

namespace Platform.Application.Abstractions.Receipts;

public interface INbrReceiptService
{
    /// <summary>
    /// Submit an order to NBR. Returns the FRN + verification URL the printed
    /// receipt should display. NULL Result means NBR integration is disabled
    /// for the current tenant (the printed receipt then shows the local
    /// order number and skips the NBR block).
    /// </summary>
    Task<NbrSubmissionResult?> SubmitAsync(int orderId, CancellationToken cancellationToken = default);
}

public sealed record NbrSubmissionResult(
    string FiscalReferenceNumber,
    string VerificationUrl,
    DateTime SubmittedAtUtc,
    string? RawResponse);
