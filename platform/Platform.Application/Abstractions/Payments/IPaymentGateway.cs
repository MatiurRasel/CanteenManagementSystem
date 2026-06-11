// =============================================================================
// IPaymentGateway  (Application abstraction)
// -----------------------------------------------------------------------------
// One impl per gateway. The orchestrator (IPaymentOrchestrator) picks the
// right impl based on PaymentMethod, calls Initiate, persists the result,
// returns a RedirectUrl. The gateway's webhook hits VerifyCallback and the
// orchestrator updates the transaction + recharges the wallet on success.
//
// SECURITY
//   - All credentials (app key, secret, callback URL) come from ITenantSettings.
//   - Webhook payloads are HMAC-verified by VerifyCallback before any DB write.
//   - The orchestrator wraps everything in the standard CQRS transaction
//     behavior so a half-recharge cannot happen.
// =============================================================================

using Platform.Domain.Payments;

namespace Platform.Application.Abstractions.Payments;

public interface IPaymentGateway
{
    PaymentMethod Method { get; }

    /// <summary>Start a charge. Returns redirect URL + gateway-side payment id.</summary>
    Task<PaymentInitiationResult> InitiateAsync(PaymentInitiationRequest request, CancellationToken cancellationToken = default);

    /// <summary>Verify a callback payload from the gateway. Returns the resolved status.</summary>
    Task<PaymentVerificationResult> VerifyCallbackAsync(string transactionRef, IDictionary<string, string> payload, CancellationToken cancellationToken = default);

    /// <summary>Refund a previously-succeeded charge. Some gateways support partial refund.</summary>
    Task<PaymentRefundResult> RefundAsync(string gatewayPaymentId, decimal amount, string reason, CancellationToken cancellationToken = default);
}

public sealed record PaymentInitiationRequest(
    string TransactionRef,
    string UserId,
    decimal Amount,
    string Currency,
    string CallbackUrl,
    string? CustomerName,
    string? CustomerPhone,
    string? CustomerEmail);

public sealed record PaymentInitiationResult(
    bool Success,
    string? GatewayPaymentId,
    string? RedirectUrl,
    string? Message);

public sealed record PaymentVerificationResult(
    PaymentStatus Status,
    string? GatewayPaymentId,
    string? Message,
    decimal? VerifiedAmount,
    string? RawPayload);

public sealed record PaymentRefundResult(bool Success, string? Message, decimal? RefundedAmount);
