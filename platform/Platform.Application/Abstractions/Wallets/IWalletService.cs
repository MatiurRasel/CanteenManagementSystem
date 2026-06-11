using Platform.Application.Results;

namespace Platform.Application.Abstractions.Wallets;

/// Reserve-first, deduct-on-delivery wallet operations. All methods append a
/// row to <c>CanteenWalletLedger</c> in the same unit of work as the balance
/// mutation so the ledger and the materialised balance stay in lock-step.
public interface IWalletService
{
    Task<Result> BlockAsync(string userId, string UserType, decimal amount, int orderId, string? idempotencyKey, CancellationToken cancellationToken = default);
    Task<Result> ReleaseAsync(string userId, string UserType, decimal amount, int orderId, string reason, CancellationToken cancellationToken = default);
    Task<Result> DeductOnDeliveryAsync(string userId, string UserType, decimal amount, int orderId, CancellationToken cancellationToken = default);
    Task<Result> RechargeAsync(string userId, string UserType, decimal amount, string source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refund a previously-deducted amount back onto the wallet. Used by the
    /// Void/Refund flow when an already-delivered order is reversed. Decreases
    /// <c>UsedBalance</c> by up to the order amount and writes a
    /// <c>WalletLedgerEntryType.Refund</c> ledger entry.
    /// </summary>
    Task<Result> RefundAsync(string userId, string UserType, decimal amount, int orderId, string reason, CancellationToken cancellationToken = default);
}
