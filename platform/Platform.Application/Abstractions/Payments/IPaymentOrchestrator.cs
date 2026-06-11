using Platform.Application.Results;
using Platform.Domain.Payments;

namespace Platform.Application.Abstractions.Payments;

/// <summary>
/// High-level facade for the payment flow:
///   1) StartRecharge persists a PaymentTransaction and asks the chosen gateway
///      for a redirect URL.
///   2) HandleCallback verifies the gateway response, updates the transaction,
///      and on Succeeded calls IWalletService.RechargeAsync.
/// </summary>
public interface IPaymentOrchestrator
{
    Task<Result<PaymentStartResponse>> StartRechargeAsync(
        string userId,
        string UserType,
        decimal amount,
        PaymentMethod method,
        string? customerName,
        string? customerPhone,
        string? customerEmail,
        CancellationToken cancellationToken = default);

    Task<Result<PaymentTransaction>> HandleCallbackAsync(
        PaymentMethod method,
        string transactionRef,
        IDictionary<string, string> payload,
        CancellationToken cancellationToken = default);
}

public sealed record PaymentStartResponse(string TransactionRef, string? RedirectUrl, string? Message);
