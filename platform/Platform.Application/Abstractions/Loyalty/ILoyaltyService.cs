// =============================================================================
// ILoyaltyService  (Platform.Application.Abstractions.Loyalty)
// -----------------------------------------------------------------------------
// Tenant-aware loyalty operations. Each call writes ONE LoyaltyEntry ledger row
// AND keeps LoyaltyAccount.PointsBalance in lock-step inside the same unit of
// work (mirrors the WalletService / WalletLedger pattern).
//
// CALLERS
//   MarkOrderDeliveredCommandHandler  → EarnAsync after wallet deduction succeeds
//   PlaceOrderCommandHandler          → RedeemAsync when the order requests points
//   Admin "adjust" page               → AdjustAsync (audit-trailed)
// =============================================================================

using Platform.Application.Results;

namespace Platform.Application.Abstractions.Loyalty;

public interface ILoyaltyService
{
    Task<Result<decimal>> EarnAsync(string userExternalId, decimal points, int orderId, string reason, CancellationToken cancellationToken = default);
    Task<Result<decimal>> RedeemAsync(string userExternalId, decimal points, int orderId, string reason, CancellationToken cancellationToken = default);
    Task<Result<decimal>> AdjustAsync(string userExternalId, decimal delta, string reason, CancellationToken cancellationToken = default);
    Task<decimal>         GetBalanceAsync(string userExternalId, CancellationToken cancellationToken = default);
}
