// =============================================================================
// MeService  (CanteenManagementSystem.Infrastructure.Me)
// -----------------------------------------------------------------------------
// IMeService default impl. Read-only repos only (ADR 0004).
// =============================================================================

using CanteenManagementSystem.Application.Me;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Orders;
using CanteenManagementSystem.Domain.Wallets;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Persistence;
using Platform.Domain.Identity;

namespace CanteenManagementSystem.Infrastructure.Me;

public sealed class MeService : IMeService
{
    private readonly IReadOnlyRepository<User> _users;
    private readonly IReadOnlyRepository<Order> _orders;
    private readonly IReadOnlyRepository<UserBalance> _balances;

    public MeService(
        IReadOnlyRepository<User> users,
        IReadOnlyRepository<Order> orders,
        IReadOnlyRepository<UserBalance> balances)
    {
        _users = users; _orders = orders; _balances = balances;
    }

    public Task<string?> ResolveLinkedPersonIdAsync(int userId, CancellationToken ct = default)
        => _users.NoTrackingQuery()
            .IgnoreQueryFilters()
            .Where(u => u.UserId == userId)
            .Select(u => u.LinkedPersonId)
            .FirstOrDefaultAsync(ct);

    public async Task<OrderHistoryPage> GetOrderHistoryAsync(int userId, int page, int pageSize, CancellationToken ct = default)
    {
        var linkedId = await ResolveLinkedPersonIdAsync(userId, ct);
        if (string.IsNullOrWhiteSpace(linkedId))
            return new OrderHistoryPage(null, Array.Empty<Order>(), 1, pageSize, 0, null, 0m);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 200);

        var qry = _orders.NoTrackingQuery()
            .Where(o => o.UserId == linkedId)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.FoodItem);

        var total = await qry.CountAsync(ct);
        var rows  = await qry.OrderByDescending(o => o.OrderDate)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var spentThisMonth = await _orders.NoTrackingQuery()
            .Where(o => o.UserId == linkedId && o.OrderDate >= monthStart && o.Status == CanteenOrderStatus.Delivered)
            .Select(o => (decimal?)o.TotalAmount).SumAsync(ct) ?? 0m;

        var balRow = await _balances.FirstOrDefaultAsync(b => b.UserId == linkedId, ct);
        decimal? balance = balRow?.AvailableBalance;

        return new OrderHistoryPage(linkedId, rows, page, pageSize, total, balance, spentThisMonth);
    }

    public async Task<IReadOnlyList<Order>> ExportAllOrdersAsync(int userId, CancellationToken ct = default)
    {
        var linkedId = await ResolveLinkedPersonIdAsync(userId, ct);
        if (string.IsNullOrWhiteSpace(linkedId)) return Array.Empty<Order>();
        return await _orders.NoTrackingQuery()
            .Where(o => o.UserId == linkedId)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.FoodItem)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync(ct);
    }
}
