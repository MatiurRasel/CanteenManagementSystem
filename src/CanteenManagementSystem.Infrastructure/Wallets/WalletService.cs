// =============================================================================
// WalletService  (CanteenManagementSystem.Infrastructure.Wallets)
// -----------------------------------------------------------------------------
// IWalletService implementation. Block / Release / Deduct / Recharge / Refund
// operate on UserBalance + WalletLedger; sends best-effort SMS on recharge.
//
// LAYERING (ADR 0004, Batch 2)
//   Uses IUnitOfWork for the multi-entity writes (UserBalance + WalletLedger).
//   Uses IReadOnlyRepository<Student> / <Employee> for the contact-phone
//   lookups (read-only). No IAppDbContext. SaveChanges is the surrounding
//   command handler's job — the TransactionBehavior in the dispatcher commits
//   the whole pipeline atomically.
// =============================================================================

using Platform.Application.Persistence;
using Platform.Application.Abstractions.Notifications;
using Platform.Application.Abstractions.RealTime;
using Platform.Application.Abstractions.Time;
using Platform.Application.Abstractions.Wallets;
using Platform.Application.Results;
using Platform.Domain.Notifications;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Users;
using CanteenManagementSystem.Domain.Wallets;
using Microsoft.Extensions.Logging;

namespace CanteenManagementSystem.Infrastructure.Wallets;

internal sealed class WalletService : IWalletService
{
    private readonly IUnitOfWork _uow;
    private readonly IReadOnlyRepository<Student> _students;
    private readonly IReadOnlyRepository<Employee> _employees;
    private readonly IClock _clock;
    private readonly INotificationService _notifications;
    private readonly IOrderBroadcaster _broadcaster;
    private readonly ILogger<WalletService> _logger;

    public WalletService(
        IUnitOfWork uow,
        IReadOnlyRepository<Student> students,
        IReadOnlyRepository<Employee> employees,
        IClock clock,
        INotificationService notifications,
        IOrderBroadcaster broadcaster,
        ILogger<WalletService> logger)
    {
        _uow = uow;
        _students = students;
        _employees = employees;
        _clock = clock;
        _notifications = notifications;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    /// <summary>
    /// Push a "wallet:balance-changed" event so the counter / parent / kiosk UI
    /// can refresh the balance badge live. Best-effort — broadcast errors must
    /// not roll back the wallet mutation.
    /// </summary>
    private async Task TryBroadcastBalanceAsync(UserBalance balance, CancellationToken ct)
    {
        try
        {
            await _broadcaster.BalanceChangedAsync(new
            {
                userId    = balance.UserId,
                userType  = balance.UserType.ToString(),
                total     = balance.TotalBalance,
                used      = balance.UsedBalance,
                blocked   = balance.BlockedAmount,
                available = balance.AvailableBalance
            }, ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Balance broadcast failed for {User}", balance.UserId); }
    }

    private IRepository<UserBalance>  Balances => _uow.Repository<UserBalance>();
    private IRepository<WalletLedger> Ledger   => _uow.Repository<WalletLedger>();

    public async Task<Result> BlockAsync(string userId, string userType, decimal amount, int orderId, string? idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (amount <= 0) return Result.Failure(Error.Validation("Amount must be positive."));

        var balance = await Balances.FirstOrDefaultAsync(
            b => b.UserId == userId && b.UserType.ToString() == userType, cancellationToken);
        if (balance is null) return Result.Failure(Error.NotFound("Wallet not found."));

        var availableIncludingEmergency = balance.AvailableBalance + balance.EmergencyAvailable;
        if (availableIncludingEmergency < amount)
        {
            return Result.Failure(Error.Conflict("Insufficient balance to block."));
        }

        balance.BlockedAmount += amount;
        balance.LastUpdated = _clock.Now;
        Balances.Update(balance);

        await Ledger.AddAsync(new WalletLedger
        {
            BalanceID = balance.BalanceID,
            UserId = userId,
            EntryType = WalletLedgerEntryType.OrderBlock,
            Amount = amount,
            BalanceAfter = balance.AvailableBalance,
            BlockedAfter = balance.BlockedAmount,
            OrderID = orderId,
            IdempotencyKey = idempotencyKey,
            Reason = "Block on order placement"
        }, cancellationToken);

        await TryBroadcastBalanceAsync(balance, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ReleaseAsync(string userId, string userType, decimal amount, int orderId, string reason, CancellationToken cancellationToken = default)
    {
        var balance = await Balances.FirstOrDefaultAsync(
            b => b.UserId == userId && b.UserType.ToString() == userType, cancellationToken);
        if (balance is null) return Result.Failure(Error.NotFound("Wallet not found."));

        var releaseAmount = Math.Min(amount, balance.BlockedAmount);
        balance.BlockedAmount -= releaseAmount;
        balance.LastUpdated = _clock.Now;
        Balances.Update(balance);

        await Ledger.AddAsync(new WalletLedger
        {
            BalanceID = balance.BalanceID,
            UserId = userId,
            EntryType = WalletLedgerEntryType.OrderRelease,
            Amount = releaseAmount,
            BalanceAfter = balance.AvailableBalance,
            BlockedAfter = balance.BlockedAmount,
            OrderID = orderId,
            Reason = reason
        }, cancellationToken);

        await TryBroadcastBalanceAsync(balance, cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeductOnDeliveryAsync(string userId, string userType, decimal amount, int orderId, CancellationToken cancellationToken = default)
    {
        var balance = await Balances.FirstOrDefaultAsync(
            b => b.UserId == userId && b.UserType.ToString() == userType, cancellationToken);
        if (balance is null) return Result.Failure(Error.NotFound("Wallet not found."));

        // Settle the previously-blocked amount first.
        var blockToConsume = Math.Min(balance.BlockedAmount, amount);
        balance.BlockedAmount -= blockToConsume;

        // Deduct main balance, falling back to emergency entitlement if needed.
        var fromMain = Math.Min(balance.AvailableBalance + blockToConsume, amount);
        balance.UsedBalance += fromMain;

        var remaining = amount - fromMain;
        if (remaining > 0)
        {
            var fromEmergency = Math.Min(balance.EmergencyAvailable, remaining);
            balance.EmergencyUsed += fromEmergency;
            remaining -= fromEmergency;
        }

        balance.LastUpdated = _clock.Now;
        Balances.Update(balance);

        await Ledger.AddAsync(new WalletLedger
        {
            BalanceID = balance.BalanceID,
            UserId = userId,
            EntryType = WalletLedgerEntryType.DeliveryDeduction,
            Amount = amount,
            BalanceAfter = balance.AvailableBalance,
            BlockedAfter = balance.BlockedAmount,
            OrderID = orderId,
            Reason = "Deduct on delivery"
        }, cancellationToken);

        await TryBroadcastBalanceAsync(balance, cancellationToken);
        return remaining > 0
            ? Result.Failure(Error.Conflict($"Insufficient funds to settle order; short by {remaining}."))
            : Result.Success();
    }

    public async Task<Result> RechargeAsync(string userId, string userType, decimal amount, string source, CancellationToken cancellationToken = default)
    {
        if (amount <= 0) return Result.Failure(Error.Validation("Amount must be positive."));

        var balance = await Balances.FirstOrDefaultAsync(
            b => b.UserId == userId && b.UserType.ToString() == userType, cancellationToken);
        if (balance is null) return Result.Failure(Error.NotFound("Wallet not found."));

        balance.TotalBalance += amount;

        // Emergency recovery: any outstanding emergency usage is repaid first.
        if (balance.EmergencyUsed > 0)
        {
            var recovered = Math.Min(balance.EmergencyUsed, amount);
            balance.EmergencyUsed -= recovered;

            await Ledger.AddAsync(new WalletLedger
            {
                BalanceID = balance.BalanceID,
                UserId = userId,
                EntryType = WalletLedgerEntryType.EmergencyRecover,
                Amount = recovered,
                BalanceAfter = balance.AvailableBalance,
                BlockedAfter = balance.BlockedAmount,
                Reason = "Emergency recovery on recharge"
            }, cancellationToken);
        }

        balance.LastUpdated = _clock.Now;
        Balances.Update(balance);

        await Ledger.AddAsync(new WalletLedger
        {
            BalanceID = balance.BalanceID,
            UserId = userId,
            EntryType = WalletLedgerEntryType.Recharge,
            Amount = amount,
            BalanceAfter = balance.AvailableBalance,
            BlockedAfter = balance.BlockedAmount,
            Reason = source
        }, cancellationToken);

        // Commit the recharge + ledger. Safe in any caller context — second
        // SaveChanges call inside a command-handler transaction is a no-op.
        await _uow.SaveChangesAsync(cancellationToken);

        // Push the new balance to live UIs (counter / parent / kiosk).
        await TryBroadcastBalanceAsync(balance, cancellationToken);

        // Best-effort SMS confirmation (never blocks the recharge).
        await TrySendRechargeSmsAsync(userId, userType, amount, balance.AvailableBalance, cancellationToken);

        return Result.Success();
    }

    public async Task<Result> RefundAsync(string userId, string userType, decimal amount, int orderId, string reason, CancellationToken cancellationToken = default)
    {
        if (amount <= 0) return Result.Failure(Error.Validation("Refund amount must be positive."));

        var balance = await Balances.FirstOrDefaultAsync(
            b => b.UserId == userId && b.UserType.ToString() == userType, cancellationToken);
        if (balance is null) return Result.Failure(Error.NotFound("Wallet not found."));

        // Reverse the earlier delivery deduction: UsedBalance was incremented
        // by the order amount; bring it back down (clamped at zero so a manual
        // adjustment can't go negative when something has reset Used to 0).
        var fromUsed = Math.Min(balance.UsedBalance, amount);
        balance.UsedBalance -= fromUsed;

        // Anything beyond what was Used (e.g. emergency-funded portion) lands
        // as a positive bump to TotalBalance so the wallet ends up whole.
        var remainder = amount - fromUsed;
        if (remainder > 0) balance.TotalBalance += remainder;

        balance.LastUpdated = _clock.Now;
        Balances.Update(balance);

        await Ledger.AddAsync(new WalletLedger
        {
            BalanceID    = balance.BalanceID,
            UserId       = userId,
            EntryType    = WalletLedgerEntryType.Refund,
            Amount       = amount,
            BalanceAfter = balance.AvailableBalance,
            BlockedAfter = balance.BlockedAmount,
            OrderID      = orderId,
            Reason       = reason
        }, cancellationToken);

        await TryBroadcastBalanceAsync(balance, cancellationToken);
        return Result.Success();
    }

    /// <summary>Send a recharge-confirmation SMS to the user (or parent contact). Failure is logged, never thrown.</summary>
    private async Task TrySendRechargeSmsAsync(string userId, string userType, decimal amount, decimal newAvailable, CancellationToken ct)
    {
        try
        {
            string? phone = userType == nameof(CanteenUserType.Student)
                ? (await _students.FirstOrDefaultAsync(s => s.ExternalId == userId, ct))?.ContactNo
                : (await _employees.FirstOrDefaultAsync(e => e.ExternalId == userId, ct))?.ContactNo;

            if (string.IsNullOrWhiteSpace(phone)) return;
            var body = $"Wallet recharged ৳{amount:N0}. New balance: ৳{newAvailable:N0}.";
            await _notifications.SendRawAsync(NotificationChannel.Sms, phone!, subject: null, body: body, cancellationToken: ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Recharge SMS failed for {User}", userId); }
    }
}
