// =============================================================================
// StripePaymentGateway  (Infrastructure.Payments.Stripe)
// -----------------------------------------------------------------------------
// Stripe Checkout Sessions via HttpClient.
//
// CREDENTIAL MAPPING (CanteenPaymentGatewayConfigs)
//   creds.AppKey    -> Stripe secret key  (sk_test_xxx / sk_live_xxx)
//   creds.AppSecret -> Stripe webhook secret (whsec_xxx)
//
// Sandbox vs live is purely the secret-key prefix.
// =============================================================================

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Platform.Application.Abstractions.Payments;
using Platform.Domain.Payments;
using Microsoft.Extensions.Logging;

namespace Platform.Infrastructure.Payments.Stripe;

public sealed class StripePaymentGateway : IPaymentGateway
{
    public PaymentMethod Method => PaymentMethod.Stripe;

    private const string BaseUrl = "https://api.stripe.com";

    private readonly HttpClient _http;
    private readonly IGatewayConfigService _gatewayConfig;
    private readonly ILogger<StripePaymentGateway> _logger;

    public StripePaymentGateway(HttpClient http, IGatewayConfigService gatewayConfig, ILogger<StripePaymentGateway> logger)
    {
        _http = http; _gatewayConfig = gatewayConfig; _logger = logger;
    }

    public async Task<PaymentInitiationResult> InitiateAsync(PaymentInitiationRequest request, CancellationToken cancellationToken = default)
    {
        var creds = await _gatewayConfig.ResolveAsync(Method, cancellationToken);
        if (creds is null || string.IsNullOrEmpty(creds.AppKey))
        {
            return new PaymentInitiationResult(false, null, null, "Stripe is not configured for this tenant.");
        }
        try
        {
            var form = new Dictionary<string, string>
            {
                ["mode"] = "payment",
                ["client_reference_id"] = request.TransactionRef,
                ["success_url"] = request.CallbackUrl + "&status=success&session_id={CHECKOUT_SESSION_ID}",
                ["cancel_url"]  = request.CallbackUrl + "&status=cancel",
                ["customer_email"] = request.CustomerEmail ?? "",
                ["payment_method_types[]"] = "card",
                ["line_items[0][quantity]"] = "1",
                ["line_items[0][price_data][currency]"] = creds.Currency.ToLowerInvariant(),
                ["line_items[0][price_data][unit_amount]"] = ((long)(request.Amount * 100)).ToString(),
                ["line_items[0][price_data][product_data][name]"] = "Canteen Recharge"
            };
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/v1/checkout/sessions")
            { Content = new FormUrlEncodedContent(form) };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", creds.AppKey);
            var resp = await _http.SendAsync(req, cancellationToken);
            var body = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                var err = body.TryGetProperty("error", out var e) && e.TryGetProperty("message", out var em) ? em.GetString() : "Stripe error";
                return new PaymentInitiationResult(false, null, null, err);
            }
            return new PaymentInitiationResult(true, body.GetProperty("id").GetString(), body.GetProperty("url").GetString(), "Stripe Checkout Session created");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stripe initiate failed for {Ref}", request.TransactionRef);
            return new PaymentInitiationResult(false, null, null, ex.Message);
        }
    }

    public async Task<PaymentVerificationResult> VerifyCallbackAsync(string transactionRef, IDictionary<string, string> payload, CancellationToken cancellationToken = default)
    {
        var creds = await _gatewayConfig.ResolveAsync(Method, cancellationToken);
        if (creds is null || string.IsNullOrEmpty(creds.AppKey))
        {
            return new PaymentVerificationResult(PaymentStatus.Failed, null, "Stripe not configured", null, JsonSerializer.Serialize(payload));
        }
        try
        {
            var sessionId = payload.TryGetValue("session_id", out var sid) ? sid : null;
            if (string.IsNullOrEmpty(sessionId))
            {
                return new PaymentVerificationResult(PaymentStatus.Failed, null, "Missing session_id", null, JsonSerializer.Serialize(payload));
            }
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/v1/checkout/sessions/{sessionId}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", creds.AppKey);
            var resp = await _http.SendAsync(req, cancellationToken);
            var body = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            if (!resp.IsSuccessStatusCode) return new PaymentVerificationResult(PaymentStatus.Failed, sessionId, "Stripe verification failed", null, body.GetRawText());
            var status = body.GetProperty("payment_status").GetString();
            var amount = body.TryGetProperty("amount_total", out var at) && at.TryGetInt64(out var minor) ? minor / 100m : (decimal?)null;
            return status == "paid"
                ? new PaymentVerificationResult(PaymentStatus.Succeeded, sessionId, "paid", amount, body.GetRawText())
                : new PaymentVerificationResult(PaymentStatus.Failed,    sessionId, status, null, body.GetRawText());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stripe verify failed for {Ref}", transactionRef);
            return new PaymentVerificationResult(PaymentStatus.Failed, null, ex.Message, null, JsonSerializer.Serialize(payload));
        }
    }

    public Task<PaymentRefundResult> RefundAsync(string gatewayPaymentId, decimal amount, string reason, CancellationToken cancellationToken = default)
        => Task.FromResult(new PaymentRefundResult(false, "Stripe refund: implement /v1/refunds when an admin UI is added.", null));
}
