// =============================================================================
// IReceiptService  (Application)
// -----------------------------------------------------------------------------
// Generates customer-facing PDF receipts and short kitchen tickets for an
// already-placed Order. Layout is QuestPDF; tenant branding (name, accent
// color, logo, address, BIN) is pulled from ITenantSettings.
//
// CALLERS
//   - /api/v1/receipts/{orderId}/pdf       (returns application/pdf bytes)
//   - /api/v1/receipts/{orderId}/kitchen   (small 80mm thermal ticket)
//   - Email channel: attaches the PDF to "order delivered" notification.
//
// VAT
//   The VAT rate is read from "VAT.Rate" tenant setting (e.g. "0.075" = 7.5%
//   under BD NBR Mushak 6.3). The receipt shows subtotal, VAT, total. If the
//   tenant is below the VAT-threshold the rate defaults to 0 and the line is
//   suppressed.
// =============================================================================

namespace Platform.Application.Abstractions.Receipts;

public interface IReceiptService
{
    Task<ReceiptPdfResult> GenerateOrderReceiptAsync(int orderId, CancellationToken cancellationToken = default);
    Task<ReceiptPdfResult> GenerateKitchenTicketAsync(int orderId, CancellationToken cancellationToken = default);
}

public sealed record ReceiptPdfResult(byte[] Bytes, string FileName, string ContentType);
