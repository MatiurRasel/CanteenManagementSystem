// =============================================================================
// SslCommerzPaymentGateway  (Infrastructure.Payments.SslCommerz)
// -----------------------------------------------------------------------------
// SSLCommerz hosted-checkout integration. Sandbox at sandbox.sslcommerz.com,
// production at securepay.sslcommerz.com. Switch via the IsSandbox flag in
// CanteenPaymentGatewayConfigs.
//
// CREDENTIAL MAPPING
//   creds.Username -> store_id
//   creds.Password -> store_passwd
//   creds.BaseUrl  -> https://sandbox.sslcommerz.com (or live)
// =============================================================================

using System.Net.Http.Json;
using System.Text.Json;
using Platform.Application.Abstractions.Payments;
using Platform.Domain.Payments;
using Microsoft.Extensions.Logging;

namespace Platform.Infrastructure.Payments.SslCommerz;

public sealed class SslCommerzPaymentGateway : IPaymentGateway
{
    public PaymentMethod Method => PaymentMethod.SslCommerz;

    private readonly HttpClient _http;
    private readonly IGatewayConfigService _gatewayConfig;
    private readonly ILogger<SslCommerzPaymentGateway> _logger;

    public SslCommerzPaymentGateway(HttpClient http, IGatewayConfigService gatewayConfig, ILogger<SslCommerzPaymentGateway> logger)
    {
        _http = http; _gatewayConfig = gatewayConfig; _logger = logger;
    }

    public async Task<PaymentInitiationResult> InitiateAsync(PaymentInitiationRequest request, CancellationToken cancellationToken = default)
    {
        var creds = await _gatewayConfig.ResolveAsync(Method, cancellationToken);
        if (creds is null || string.IsNullOrEmpty(creds.Username))
        {
            return new PaymentInitiationResult(false, null, null, "SSLCommerz is not configured for this tenant.");
        }
        try
        {
            var form = new Dictionary<string, string>
            {
                ["store_id"] = creds.Username,
                ["store_passwd"] = creds.Password ?? "",
                ["total_amount"] = request.Amount.ToString("F2"),
                ["currency"] = creds.Currency,
                ["tran_id"] = request.TransactionRef,
                ["success_url"] = request.CallbackUrl + "&status=success",
                ["fail_url"] = request.CallbackUrl + "&status=fail",
                ["cancel_url"] = request.CallbackUrl + "&status=cancel",
                ["ipn_url"] = request.CallbackUrl + "&channel=ipn",
                ["cus_name"] = request.CustomerName ?? "Canteen User",
                ["cus_email"] = request.CustomerEmail ?? "no-reply@example.com",
                ["cus_phone"] = request.CustomerPhone ?? "01700000000",
                ["cus_add1"] = "N/A",
                ["cus_city"] = "Dhaka",
                ["cus_country"] = "Bangladesh",
                ["shipping_method"] = "NO",
                ["product_name"] = "Canteen Recharge",
                ["product_category"] = "Service",
                ["product_profile"] = "general"
            };

            using var content = new FormUrlEncodedContent(form);
            var resp = await _http.PostAsync($"{creds.BaseUrl}/gwprocess/v4/api.php", content, cancellationToken);
            var body = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            var status = body.TryGetProperty("status", out var s) ? s.GetString() : null;
            if (status == "SUCCESS")
            {
                return new PaymentInitiationResult(true,
                    body.TryGetProperty("sessionkey", out var sk) ? sk.GetString() : null,
                    body.GetProperty("GatewayPageURL").GetString(),
                    "Redirect to hosted page");
            }
            return new PaymentInitiationResult(false, null, null,
                body.TryGetProperty("failedreason", out var fr) ? fr.GetString() : "SSLCommerz init failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSLCommerz initiate failed for {Ref}", request.TransactionRef);
            return new PaymentInitiationResult(false, null, null, ex.Message);
        }
    }

    public async Task<PaymentVerificationResult> VerifyCallbackAsync(string transactionRef, IDictionary<string, string> payload, CancellationToken cancellationToken = default)
    {
        var creds = await _gatewayConfig.ResolveAsync(Method, cancellationToken);
        if (creds is null || string.IsNullOrEmpty(creds.Username))
        {
            return new PaymentVerificationResult(PaymentStatus.Failed, null, "SSLCommerz not configured", null, JsonSerializer.Serialize(payload));
        }
        try
        {
            var valId = payload.TryGetValue("val_id", out var v) ? v : null;
            if (string.IsNullOrEmpty(valId))
            {
                return new PaymentVerificationResult(PaymentStatus.Failed, null, "Missing val_id", null, JsonSerializer.Serialize(payload));
            }
            var url = $"{creds.BaseUrl}/validator/api/validationserverAPI.php?val_id={Uri.EscapeDataString(valId)}&store_id={Uri.EscapeDataString(creds.Username)}&store_passwd={Uri.EscapeDataString(creds.Password ?? "")}&format=json";
            var resp = await _http.GetAsync(url, cancellationToken);
            var body = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            var status = body.TryGetProperty("status", out var s) ? s.GetString() : null;
            var raw = body.GetRawText();
            var amount = decimal.TryParse(body.TryGetProperty("amount", out var a) ? a.GetString() : "0", out var ad) ? ad : (decimal?)null;
            return status is "VALID" or "VALIDATED"
                ? new PaymentVerificationResult(PaymentStatus.Succeeded, body.TryGetProperty("tran_id", out var tid) ? tid.GetString() : valId, "OK", amount, raw)
                : new PaymentVerificationResult(PaymentStatus.Failed, valId, status, null, raw);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SSLCommerz verify failed for {Ref}", transactionRef);
            return new PaymentVerificationResult(PaymentStatus.Failed, null, ex.Message, null, JsonSerializer.Serialize(payload));
        }
    }

    public Task<PaymentRefundResult> RefundAsync(string gatewayPaymentId, decimal amount, string reason, CancellationToken cancellationToken = default)
        => Task.FromResult(new PaymentRefundResult(false, "SSLCommerz refund must be raised via merchant portal", null));
}
