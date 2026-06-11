// =============================================================================
// LoyaltyService  (CanteenManagementSystem.Infrastructure.Loyalty)
// -----------------------------------------------------------------------------
// Materialised-cache balance + append-only ledger. Idempotency relies on
// per-call uniqueness of (UserExternalId, OrderId, EntryType) — repeat
// invocations for the same order are detected and skipped.
//
// ADR 0004: IUnitOfWork + IRepository<T> / IReadOnlyRepository<T>.
// =============================================================================

using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions.Loyalty;
using Platform.Application.Abstractions.Time;
using Platform.Application.Persistence;
using Platform.Application.Results;
using Platform.Domain.Loyalty;

namespace CanteenManagementSystem.Infrastructure.Loyalty;

internal sealed class LoyaltyService : ILoyaltyService
{
    private readonly IUnitOfWork _uow;
    private readonly IReadOnlyRepository<LoyaltyAccount> _accountsReader;
    private readonly IClock _clock;

    public LoyaltyService(
        IUnitOfWork uow,
        IReadOnlyRepository<LoyaltyAccount> accountsReader,
        IClock clock)
    {
        _uow = uow;
        _accountsReader = accountsReader;
        _clock = clock;
    }

    private IRepository<LoyaltyAccount> Accounts => _uow.Repository<LoyaltyAccount>();
    private IRepository<LoyaltyEntry>   Entries  => _uow.Repository<LoyaltyEntry>();

    public Task<Result<decimal>> EarnAsync(string userId, decimal points, int orderId, string reason, CancellationToken ct = default)
        => MoveAsync(userId, points, orderId, reason, LoyaltyEntryType.Earn, ct);

    public Task<Result<decimal>> RedeemAsync(string userId, decimal points, int orderId, string reason, CancellationToken ct = default)
        => MoveAsync(userId, -Math.Abs(points), orderId, reason, LoyaltyEntryType.Spend, ct);

    public async Task<Result<decimal>> AdjustAsync(string userId, decimal delta, string reason, CancellationToken ct = default)
        => await MoveAsync(userId, delta, orderId: null, reason, LoyaltyEntryType.Adjust, ct);

    public async Task<decimal> GetBalanceAsync(string userId, CancellationToken ct = default)
        => (await _accountsReader.FirstOrDefaultAsync(a => a.UserExternalId == userId, ct))?.PointsBalance ?? 0m;

    private async Task<Result<decimal>> MoveAsync(string userId, decimal delta, int? orderId, string reason,
        LoyaltyEntryType type, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId)) return Result.Failure<decimal>(Error.Validation("UserExternalId required."));
        if (delta == 0m) return Result.Failure<decimal>(Error.Validation("Delta must be non-zero."));

        // Idempotency for order-bound moves: any existing entry for this OrderId+Type
        // tied to an account whose UserExternalId matches means we've already booked.
        if (orderId is int oid && type != LoyaltyEntryType.Adjust)
        {
            var already = await Entries.NoTrackingQuery()
                .AnyAsync(e => e.OrderId == oid && e.EntryType == type
                            && Accounts.NoTrackingQuery()
                                  .Where(a => a.LoyaltyAccountId == e.LoyaltyAccountId && a.UserExternalId == userId)
                                  .Any(), ct);
            if (already)
            {
                // Return current balance, no-op.
                var current = await GetBalanceAsync(userId, ct);
                return Result.Success(current);
            }
        }

        var account = await Accounts.FirstOrDefaultAsync(a => a.UserExternalId == userId, ct);
        if (account is null)
        {
            account = new LoyaltyAccount { UserExternalId = userId, PointsBalance = 0m, CreatedAtUtc = _clock.UtcNow };
            await Accounts.AddAsync(account, ct);
            await _uow.SaveChangesAsync(ct);
        }

        if (delta < 0 && account.PointsBalance + delta < 0)
            return Result.Failure<decimal>(Error.Conflict("Insufficient loyalty balance."));

        account.PointsBalance += delta;
        if (type == LoyaltyEntryType.Earn)  account.LastEarnedAtUtc   = _clock.UtcNow;
        if (type == LoyaltyEntryType.Spend) account.LastRedeemedAtUtc = _clock.UtcNow;

        await Entries.AddAsync(new LoyaltyEntry
        {
            LoyaltyAccountId = account.LoyaltyAccountId,
            EntryType        = type,
            Delta            = delta,
            BalanceAfter     = account.PointsBalance,
            OrderId          = orderId,
            Reason           = reason,
            OccurredAtUtc    = _clock.UtcNow
        }, ct);
        return Result.Success(account.PointsBalance);
    }
}
