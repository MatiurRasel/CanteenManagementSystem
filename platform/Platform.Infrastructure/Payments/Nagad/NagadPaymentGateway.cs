// =============================================================================
// NagadPaymentGateway  (Infrastructure.Payments.Nagad)
// -----------------------------------------------------------------------------
// Nagad direct merchant integration. Requires RSA key pair (held in
// CanteenPaymentGatewayConfigs.Live/SandboxPublicKey + PrivateKey columns).
// Until the RSA signing helpers land, this gateway returns a graceful
// "not yet configured" response so the orchestrator records a clear failure.
// =============================================================================

using System.Text.Json;
using Platform.Application.Abstractions.Payments;
using Platform.Domain.Payments;
using Microsoft.Extensions.Logging;

namespace Platform.Infrastructure.Payments.Nagad;

public sealed class NagadPaymentGateway : IPaymentGateway
{
    public PaymentMethod Method => PaymentMethod.Nagad;

    private readonly HttpClient _http;
    private readonly IGatewayConfigService _gatewayConfig;
    private readonly ILogger<NagadPaymentGateway> _logger;

    public NagadPaymentGateway(HttpClient http, IGatewayConfigService gatewayConfig, ILogger<NagadPaymentGateway> logger)
    {
        _http = http; _gatewayConfig = gatewayConfig; _logger = logger;
    }

    public async Task<PaymentInitiationResult> InitiateAsync(PaymentInitiationRequest request, CancellationToken cancellationToken = default)
    {
        var creds = await _gatewayConfig.ResolveAsync(Method, cancellationToken);
        if (creds is null || string.IsNullOrEmpty(creds.MerchantId) || string.IsNullOrEmpty(creds.PrivateKey))
        {
            return new PaymentInitiationResult(false, null, null,
                "Nagad is not configured for this tenant (merchant id + RSA keys required).");
        }

        // PRODUCTION TODO: implement the full Nagad signed payload here.
        //   1. POST /api/dfs/check-out/initialize/{merchantId}/{orderId} with
        //      base64(JSON{ accountNumber, datetime, sensitiveData(rsa), signature(rsa) })
        //   2. Receive sensitiveData -> RSA-decrypt -> paymentReferenceId.
        //   3. POST /api/dfs/check-out/complete/{paymentReferenceId} with
        //      encrypted + signed body -> callBackUrl.
        _ = _http;
        return new PaymentInitiationResult(false, null, null, "Nagad implementation pending RSA signing helpers.");
    }

    public Task<PaymentVerificationResult> VerifyCallbackAsync(string transactionRef, IDictionary<string, string> payload, CancellationToken cancellationToken = default)
        => Task.FromResult(new PaymentVerificationResult(PaymentStatus.Failed, null,
            "Nagad verify pending RSA signing helpers", null, JsonSerializer.Serialize(payload)));

    public Task<PaymentRefundResult> RefundAsync(string gatewayPaymentId, decimal amount, string reason, CancellationToken cancellationToken = default)
        => Task.FromResult(new PaymentRefundResult(false, "Nagad refunds go through merchant portal", null));
}
