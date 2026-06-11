// =============================================================================
// AdminExportsService  (CanteenManagementSystem.Infrastructure.Exports)
// -----------------------------------------------------------------------------
// EF Core impl of the /admin/exports/* aggregator. Tenant-scoped through the
// global query filter; never bypasses it.
// =============================================================================

using CanteenManagementSystem.Application.Exports;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Orders;
using CanteenManagementSystem.Domain.Wallets;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Persistence;

namespace CanteenManagementSystem.Infrastructure.Exports;

public sealed class AdminExportsService : IAdminExportsService
{
    private readonly IAppDbContext _db;
    public AdminExportsService(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<OrderExportRow>> GetOrdersAsync(
        DateTime fromUtc, DateTime toUtcExclusive,
        CanteenOrderStatus? status, int maxRows,
        CancellationToken ct = default)
    {
        var q = _db.Set<Order>()
            .AsNoTracking()
            .Where(o => o.OrderDate >= fromUtc && o.OrderDate < toUtcExclusive);
        if (status.HasValue) q = q.Where(o => o.Status == status.Value);

        return await q
            .OrderByDescending(o => o.OrderDate)
            .Select(o => new OrderExportRow(
                o.OrderNumber,
                o.OrderDate,
                o.DeliveredDate,
                o.Status,
                o.UserId,
                o.UserType,
                o.TotalAmount,
                o.IsPreOrder,
                o.PickupAtUtc,
                o.TableNumber,
                string.Join("; ",
                    o.OrderItems.Select(i => i.Quantity + "× " + (i.FoodItem != null ? i.FoodItem.ItemName : "(item)")))))
            .Take(maxRows)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WalletLedgerExportRow>> GetWalletLedgerAsync(
        DateTime fromUtc, DateTime toUtcExclusive,
        string? userId, int maxRows,
        CancellationToken ct = default)
    {
        var q = _db.Set<WalletLedger>()
            .AsNoTracking()
            .Where(l => l.CreatedAtUtc >= fromUtc && l.CreatedAtUtc < toUtcExclusive);
        if (!string.IsNullOrWhiteSpace(userId)) q = q.Where(l => l.UserId == userId);

        return await q
            .OrderByDescending(l => l.CreatedAtUtc)
            .Select(l => new WalletLedgerExportRow(
                l.LedgerID,
                l.UserId,
                l.EntryType.ToString(),
                l.Amount,
                l.BalanceAfter,
                l.BlockedAfter,
                l.OrderID,
                l.Reason,
                l.CreatedAtUtc,
                l.CreatedBy,
                l.IdempotencyKey))
            .Take(maxRows)
            .ToListAsync(ct);
    }
}
