// =============================================================================
// PaymentOrchestrator  (Infrastructure.Payments)
// -----------------------------------------------------------------------------
// Coordinates the multi-step gateway flow:
//
//   START
//     1. Validate inputs.
//     2. Insert PaymentTransaction (status=Initiated).
//     3. Resolve IPaymentGateway by PaymentMethod.
//     4. Call gateway.InitiateAsync -> get RedirectUrl.
//     5. Update transaction (status=Pending, GatewayPaymentId, RedirectUrl).
//     6. Return RedirectUrl to controller, which 302s the user.
//
//   CALLBACK
//     1. Lookup transaction by TransactionRef. Reject if missing or terminal.
//     2. Resolve gateway, call VerifyCallbackAsync(payload).
//     3. Persist verification result.
//     4. If Succeeded -> IWalletService.RechargeAsync (idempotent via Ref).
//     5. IAuditTrail records "Payment.Succeeded" or "Payment.Failed".
//
// ADR 0004: IUnitOfWork + IRepository<PaymentTransaction>.
// =============================================================================

using Platform.Application.Persistence;
using Platform.Application.Abstractions.Audit;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Abstractions.Identity;
using Platform.Application.Abstractions.Payments;
using Platform.Application.Abstractions.Time;
using Platform.Application.Abstractions.Wallets;
using Platform.Application.Results;
using Platform.Domain.Payments;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Platform.Infrastructure.Payments;

public sealed class PaymentOrchestrator : IPaymentOrchestrator
{
    private readonly IUnitOfWork _uow;
    private readonly IEnumerable<IPaymentGateway> _gateways;
    private readonly IWalletService _wallet;
    private readonly IAuditTrail _audit;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly ITenantSettings _settings;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<PaymentOrchestrator> _logger;

    public PaymentOrchestrator(
        IUnitOfWork uow,
        IEnumerable<IPaymentGateway> gateways,
        IWalletService wallet,
        IAuditTrail audit,
        ICurrentUser user,
        IClock clock,
        ITenantSettings settings,
        IHttpContextAccessor http,
        ILogger<PaymentOrchestrator> logger)
    {
        _uow = uow; _gateways = gateways; _wallet = wallet; _audit = audit;
        _user = user; _clock = clock; _settings = settings; _http = http;
        _logger = logger;
    }

    private IRepository<PaymentTransaction> Transactions => _uow.Repository<PaymentTransaction>();

    public async Task<Result<PaymentStartResponse>> StartRechargeAsync(
        string userId,
        string userType,
        decimal amount,
        PaymentMethod method,
        string? customerName,
        string? customerPhone,
        string? customerEmail,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0) return Result.Failure<PaymentStartResponse>(Error.Validation("Amount must be positive."));
        var gateway = _gateways.FirstOrDefault(g => g.Method == method);
        if (gateway is null) return Result.Failure<PaymentStartResponse>(Error.Failure($"No gateway registered for {method}."));

        var transaction = new PaymentTransaction
        {
            UserId = userId,
            UserType = userType,
            Amount = amount,
            Method = method,
            Status = PaymentStatus.Initiated,
            InitiatedBy = _user.UserId,
            CreatedAtUtc = _clock.UtcNow
        };
        await Transactions.AddAsync(transaction, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        var callbackBase = await _settings.GetAsync("Payments.CallbackBaseUrl", BuildBaseUrl(), cancellationToken) ?? BuildBaseUrl();
        var callbackUrl = $"{callbackBase.TrimEnd('/')}/api/v1/payments/{method.ToString().ToLowerInvariant()}/callback?ref={transaction.TransactionRef}";

        var initiation = await gateway.InitiateAsync(new PaymentInitiationRequest(
            transaction.TransactionRef, userId, amount, transaction.Currency, callbackUrl,
            customerName, customerPhone, customerEmail), cancellationToken);

        transaction.GatewayPaymentId = initiation.GatewayPaymentId;
        transaction.GatewayMessage   = initiation.Message;
        transaction.RedirectUrl      = initiation.RedirectUrl;
        transaction.Status           = initiation.Success ? PaymentStatus.Pending : PaymentStatus.Failed;
        if (!initiation.Success) transaction.CompletedAtUtc = _clock.UtcNow;
        await _uow.SaveChangesAsync(cancellationToken);

        await _audit.RecordAsync("Payment.Started", nameof(PaymentTransaction), transaction.TransactionRef,
            new { userId, amount, method = method.ToString(), success = initiation.Success }, cancellationToken);

        return initiation.Success
            ? Result.Success(new PaymentStartResponse(transaction.TransactionRef, initiation.RedirectUrl, initiation.Message))
            : Result.Failure<PaymentStartResponse>(Error.Failure(initiation.Message ?? "Gateway rejected the payment."));
    }

    public async Task<Result<PaymentTransaction>> HandleCallbackAsync(
        PaymentMethod method,
        string transactionRef,
        IDictionary<string, string> payload,
        CancellationToken cancellationToken = default)
    {
        var transaction = await Transactions.FirstOrDefaultAsync(
            t => t.TransactionRef == transactionRef && t.Method == method, cancellationToken);
        if (transaction is null) return Result.Failure<PaymentTransaction>(Error.NotFound("Payment not found."));
        if (transaction.Status is PaymentStatus.Succeeded or PaymentStatus.Refunded)
        {
            // Idempotent callback — gateway sometimes retries.
            return Result.Success(transaction);
        }

        var gateway = _gateways.First(g => g.Method == method);
        var verification = await gateway.VerifyCallbackAsync(transactionRef, payload, cancellationToken);

        transaction.Status           = verification.Status;
        transaction.GatewayPaymentId = verification.GatewayPaymentId ?? transaction.GatewayPaymentId;
        transaction.GatewayMessage   = verification.Message;
        transaction.CallbackPayload  = verification.RawPayload;
        transaction.CompletedAtUtc   = _clock.UtcNow;
        await _uow.SaveChangesAsync(cancellationToken);

        if (verification.Status == PaymentStatus.Succeeded)
        {
            // The wallet ledger reuses the TransactionRef as idempotency key so
            // a duplicate callback can't double-recharge.
            var rechargeResult = await _wallet.RechargeAsync(transaction.UserId, transaction.UserType,
                verification.VerifiedAmount ?? transaction.Amount,
                source: $"{method} #{transaction.TransactionRef}",
                cancellationToken);

            await _uow.SaveChangesAsync(cancellationToken);

            await _audit.RecordAsync("Payment.Succeeded", nameof(PaymentTransaction), transactionRef,
                new { transaction.UserId, transaction.Amount, method = method.ToString() }, cancellationToken);

            if (rechargeResult.IsFailure)
            {
                _logger.LogError("Recharge failed after a successful payment {Ref}: {Error}", transactionRef, rechargeResult.Error.Message);
            }
        }
        else
        {
            await _audit.RecordAsync("Payment.Failed", nameof(PaymentTransaction), transactionRef,
                new { method = method.ToString(), verification.Message }, cancellationToken);
        }

        return Result.Success(transaction);
    }

    private string BuildBaseUrl()
    {
        var req = _http.HttpContext?.Request;
        if (req is null) return "https://example.com";
        return $"{req.Scheme}://{req.Host}";
    }
}
