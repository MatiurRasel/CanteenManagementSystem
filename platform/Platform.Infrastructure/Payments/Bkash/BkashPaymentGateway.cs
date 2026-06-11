// =============================================================================
// BkashPaymentGateway  (Infrastructure.Payments.Bkash)
// -----------------------------------------------------------------------------
// HttpClient-based bKash Tokenized Checkout integration. Sandbox at
// https://tokenized.sandbox.bka.sh/v1.2.0-beta — flip via the CanteenPaymentGatewayConfigs
// row's IsSandbox flag, not via a redeploy.
//
// CREDENTIAL SOURCE (priority order)
//   IGatewayConfigService.ResolveAsync(Bkash) -> reads CanteenPaymentGatewayConfigs row
//     -> projects either Sandbox* or Live* columns based on IsSandbox.
//
// FLOW (Tokenized Checkout)
//   1. POST /tokenized/checkout/token/grant            -> id_token (cache 50m)
//   2. POST /tokenized/checkout/create                 -> paymentID + bkashURL
//   3. user authorizes on bkashURL, gateway redirects to our callback
//   4. POST /tokenized/checkout/execute                -> transaction confirmed
//
// CACHE
//   The id_token is cached under "payments:bkash:token:{configId}" for 50 min
//   so concurrent requests don't burn the rate limit.
// =============================================================================

using System.Net.Http.Json;
using System.Text.Json;
using Platform.Application.Abstractions.Caching;
using Platform.Application.Abstractions.Payments;
using Platform.Domain.Payments;
using Microsoft.Extensions.Logging;

namespace Platform.Infrastructure.Payments.Bkash;

public sealed class BkashPaymentGateway : IPaymentGateway
{
    public PaymentMethod Method => PaymentMethod.Bkash;

    private readonly HttpClient _http;
    private readonly IGatewayConfigService _gatewayConfig;
    private readonly ICacheService _cache;
    private readonly ILogger<BkashPaymentGateway> _logger;

    public BkashPaymentGateway(HttpClient http, IGatewayConfigService gatewayConfig, ICacheService cache, ILogger<BkashPaymentGateway> logger)
    {
        _http = http; _gatewayConfig = gatewayConfig; _cache = cache; _logger = logger;
    }

    public async Task<PaymentInitiationResult> InitiateAsync(PaymentInitiationRequest request, CancellationToken cancellationToken = default)
    {
        var creds = await _gatewayConfig.ResolveAsync(Method, cancellationToken);
        if (creds is null || string.IsNullOrEmpty(creds.AppKey))
        {
            return new PaymentInitiationResult(false, null, null, "bKash is not configured for this tenant.");
        }

        try
        {
            var token = await GetTokenAsync(creds, cancellationToken);
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{creds.BaseUrl}/tokenized/checkout/create");
            req.Headers.Add("Authorization", token);
            req.Headers.Add("X-APP-Key", creds.AppKey);
            req.Content = JsonContent.Create(new
            {
                mode = "0011",
                payerReference = request.UserId,
                callbackURL = request.CallbackUrl,
                amount = request.Amount.ToString("F2"),
                currency = creds.Currency,
                intent = "sale",
                merchantInvoiceNumber = request.TransactionRef
            });

            var resp = await _http.SendAsync(req, cancellationToken);
            var body = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);

            var statusCode = body.TryGetProperty("statusCode", out var sc) ? sc.GetString() : null;
            if (statusCode != "0000")
            {
                var msg = body.TryGetProperty("statusMessage", out var sm) ? sm.GetString() : "bKash create failed";
                return new PaymentInitiationResult(false, null, null, msg);
            }
            return new PaymentInitiationResult(true,
                GatewayPaymentId: body.GetProperty("paymentID").GetString(),
                RedirectUrl: body.GetProperty("bkashURL").GetString(),
                Message: "Created");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "bKash initiate failed for {Ref}", request.TransactionRef);
            return new PaymentInitiationResult(false, null, null, ex.Message);
        }
    }

    public async Task<PaymentVerificationResult> VerifyCallbackAsync(string transactionRef, IDictionary<string, string> payload, CancellationToken cancellationToken = default)
    {
        var creds = await _gatewayConfig.ResolveAsync(Method, cancellationToken);
        if (creds is null || string.IsNullOrEmpty(creds.AppKey))
        {
            return new PaymentVerificationResult(PaymentStatus.Failed, null, "bKash not configured", null, JsonSerializer.Serialize(payload));
        }

        try
        {
            var paymentId = payload.TryGetValue("paymentID", out var pid) ? pid : null;
            if (string.IsNullOrEmpty(paymentId))
            {
                return new PaymentVerificationResult(PaymentStatus.Failed, null, "Missing paymentID", null, JsonSerializer.Serialize(payload));
            }

            var token = await GetTokenAsync(creds, cancellationToken);
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{creds.BaseUrl}/tokenized/checkout/execute");
            req.Headers.Add("Authorization", token);
            req.Headers.Add("X-APP-Key", creds.AppKey);
            req.Content = JsonContent.Create(new { paymentID = paymentId });

            var resp = await _http.SendAsync(req, cancellationToken);
            var body = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);

            var statusCode = body.TryGetProperty("statusCode", out var sc) ? sc.GetString() : null;
            var rawJson = body.GetRawText();
            if (statusCode == "0000" || (body.TryGetProperty("transactionStatus", out var ts) && ts.GetString() == "Completed"))
            {
                var amount = decimal.TryParse(body.TryGetProperty("amount", out var a) ? a.GetString() : "0", out var ad) ? ad : (decimal?)null;
                return new PaymentVerificationResult(PaymentStatus.Succeeded, paymentId, "Completed", amount, rawJson);
            }
            var msg = body.TryGetProperty("statusMessage", out var sm) ? sm.GetString() : "bKash execute failed";
            return new PaymentVerificationResult(PaymentStatus.Failed, paymentId, msg, null, rawJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "bKash verify failed for {Ref}", transactionRef);
            return new PaymentVerificationResult(PaymentStatus.Failed, null, ex.Message, null, JsonSerializer.Serialize(payload));
        }
    }

    public Task<PaymentRefundResult> RefundAsync(string gatewayPaymentId, decimal amount, string reason, CancellationToken cancellationToken = default)
        => Task.FromResult(new PaymentRefundResult(false, "bKash refund is operator-mediated; raise via the bKash merchant portal.", null));

    // -------------------- token grant w/ caching ---------------------------
    private async Task<string> GetTokenAsync(GatewayCredentials creds, CancellationToken cancellationToken)
    {
        var cacheKey = $"payments:bkash:token:{creds.GatewayConfigId}";
        var box = await _cache.GetOrSetAsync(cacheKey, async ct =>
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{creds.BaseUrl}/tokenized/checkout/token/grant");
            req.Headers.Add("username", creds.Username);
            req.Headers.Add("password", creds.Password);
            req.Content = JsonContent.Create(new { app_key = creds.AppKey, app_secret = creds.AppSecret });

            var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
            var idToken = body.GetProperty("id_token").GetString();
            return new BkashTokenBox(idToken ?? throw new InvalidOperationException("bKash returned an empty id_token."));
        }, ttl: TimeSpan.FromMinutes(50), cancellationToken: cancellationToken);
        return box.Token;
    }

    private sealed class BkashTokenBox
    {
        public string Token { get; }
        public BkashTokenBox(string token) => Token = token;
    }
}
