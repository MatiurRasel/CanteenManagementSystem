// =============================================================================
// StubNbrReceiptService  (CanteenManagementSystem.Infrastructure.Receipts)
// -----------------------------------------------------------------------------
// Bypass impl. Returns NULL whenever `Nbr.Enabled` is false (the default), so
// the receipt printer + PDF generator skip the NBR block without touching the
// gateway. When `Nbr.Enabled = true` BUT no MerchantToken is configured, this
// stub logs a warning and returns NULL so the canteen can still operate while
// NBR onboarding is pending.
//
// Replace with `RealNbrReceiptService` once credentials are issued — that impl
// HTTP-POSTs the Mushak 6.3 XML payload to `Nbr.GatewayBaseUrl`, signs with
// the tenant's `Nbr.SigningKey`, parses the FRN out of the response.
// =============================================================================

using Microsoft.Extensions.Logging;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Abstractions.Receipts;

namespace CanteenManagementSystem.Infrastructure.Receipts;

public sealed class StubNbrReceiptService : INbrReceiptService
{
    private readonly ITenantSettings _settings;
    private readonly ILogger<StubNbrReceiptService> _logger;

    public StubNbrReceiptService(ITenantSettings settings, ILogger<StubNbrReceiptService> logger)
    {
        _settings = settings; _logger = logger;
    }

    public async Task<NbrSubmissionResult?> SubmitAsync(int orderId, CancellationToken cancellationToken = default)
    {
        var enabled = await _settings.GetBoolAsync("Nbr.Enabled", false, cancellationToken);
        if (!enabled) return null;

        var token = await _settings.GetAsync("Nbr.MerchantToken", defaultValue: null, cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Nbr.Enabled=true but Nbr.MerchantToken empty — skipping NBR submission for order {Order}.", orderId);
            return null;
        }

        // ── Real impl would POST to Nbr.GatewayBaseUrl here. ────────────
        // For the stub, fabricate a deterministic-looking FRN so the printed
        // receipt design can be exercised end-to-end on a sandbox tenant.
        var bin = await _settings.GetAsync("Nbr.Bin", "0000000-0000", cancellationToken);
        var fakeFrn = $"FRN-{bin}-{DateTime.UtcNow:yyyyMMddHHmmss}-{orderId:D6}";
        var verifyUrl = $"https://example-emushak/verify?frn={Uri.EscapeDataString(fakeFrn)}";
        _logger.LogInformation("NBR stub returned FRN {Frn} for order {Order} (no real submission).", fakeFrn, orderId);

        return new NbrSubmissionResult(
            FiscalReferenceNumber: fakeFrn,
            VerificationUrl:       verifyUrl,
            SubmittedAtUtc:        DateTime.UtcNow,
            RawResponse:           "(stub)");
    }
}
