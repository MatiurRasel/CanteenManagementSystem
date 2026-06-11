// =============================================================================
// ParentPortalService  (CanteenManagementSystem.Infrastructure.Parents)
// -----------------------------------------------------------------------------
// IParentPortalService impl. Repos only — no IAppDbContext. Card freeze
// includes audit + CardEvent in one UoW commit.
// =============================================================================

using CanteenManagementSystem.Application.Parents;
using CanteenManagementSystem.Domain.Cards;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Orders;
using CanteenManagementSystem.Domain.Users;
using CanteenManagementSystem.Domain.Wallets;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions.Audit;
using Platform.Application.Persistence;
using Platform.Application.Results;
using Platform.Domain.Identity;

namespace CanteenManagementSystem.Infrastructure.Parents;

public sealed class ParentPortalService : IParentPortalService
{
    private readonly IReadOnlyRepository<User>        _users;
    private readonly IReadOnlyRepository<Student>     _students;
    private readonly IReadOnlyRepository<UserBalance> _balances;
    private readonly IReadOnlyRepository<Order>       _orders;
    private readonly IRepository<NfcCard>             _cards;
    private readonly IRepository<CardEvent>           _cardEvents;
    private readonly IUnitOfWork _uow;
    private readonly IAuditTrail _audit;

    public ParentPortalService(
        IReadOnlyRepository<User> users,
        IReadOnlyRepository<Student> students,
        IReadOnlyRepository<UserBalance> balances,
        IReadOnlyRepository<Order> orders,
        IRepository<NfcCard> cards,
        IRepository<CardEvent> cardEvents,
        IUnitOfWork uow, IAuditTrail audit)
    {
        _users = users; _students = students; _balances = balances; _orders = orders;
        _cards = cards; _cardEvents = cardEvents; _uow = uow; _audit = audit;
    }

    public async Task<IReadOnlyList<string>> GetLinkedChildIdsAsync(int userId, CancellationToken ct = default)
    {
        var csv = await _users.NoTrackingQuery().IgnoreQueryFilters()
            .Where(u => u.UserId == userId)
            .Select(u => u.LinkedChildrenCsv)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(csv)) return Array.Empty<string>();
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    public async Task<IReadOnlyList<ParentChildSnapshot>> BuildDashboardAsync(IReadOnlyList<string> linkedIds, CancellationToken ct = default)
    {
        if (linkedIds.Count == 0) return Array.Empty<ParentChildSnapshot>();
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var output = new List<ParentChildSnapshot>(linkedIds.Count);

        foreach (var externalId in linkedIds)
        {
            var s = await _students.NoTrackingQuery()
                .Where(x => x.ExternalId == externalId)
                .Select(x => new { x.ExternalId, x.Name, x.Program, x.ContactNo })
                .FirstOrDefaultAsync(ct);
            if (s is null) continue;

            var balance = await _balances.NoTrackingQuery()
                .Where(b => b.UserId == externalId && b.UserType == CanteenUserType.Student)
                .Select(b => (decimal?)b.AvailableBalance).FirstOrDefaultAsync(ct) ?? 0m;

            var spent = await _orders.NoTrackingQuery()
                .Where(o => o.UserId == externalId && o.OrderDate >= monthStart && o.Status == CanteenOrderStatus.Delivered)
                .Select(o => (decimal?)o.TotalAmount).SumAsync(ct) ?? 0m;

            var card = await _cards.NoTrackingQuery()
                .Where(c => c.UserId == externalId && c.UserType == CanteenUserType.Student
                            && (c.Status == CardStatus.Active || c.Status == CardStatus.Blocked))
                .OrderByDescending(c => c.IssuedAtUtc)
                .Select(c => new { c.CardId, c.CardUid, c.Status })
                .FirstOrDefaultAsync(ct);

            var recent = await _orders.NoTrackingQuery()
                .Where(o => o.UserId == externalId)
                .Include(o => o.OrderItems).ThenInclude(oi => oi.FoodItem)
                .OrderByDescending(o => o.OrderDate).Take(8).ToListAsync(ct);

            output.Add(new ParentChildSnapshot(
                s.ExternalId, s.Name, s.Program, s.ContactNo,
                balance, spent,
                card?.CardId, card?.CardUid, card?.Status,
                recent));
        }
        return output;
    }

    public async Task<Result> FreezeChildCardAsync(int cardId, IReadOnlyList<string> allowedExternalIds, string? performedBy, CancellationToken ct = default)
    {
        var card = await _cards.FirstOrDefaultAsync(c => c.CardId == cardId, ct);
        if (card is null) return Result.Failure(Error.NotFound("Card not found."));
        if (!allowedExternalIds.Contains(card.UserId, StringComparer.Ordinal))
            return Result.Failure(Error.Unauthorized("Not linked to that child."));
        if (card.Status != CardStatus.Active)
            return Result.Failure(Error.Validation("Card already inactive."));

        var now = DateTime.UtcNow;
        card.Status = CardStatus.Blocked;
        card.BlockedAtUtc = now;
        _cards.Update(card);

        await _cardEvents.AddAsync(new CardEvent
        {
            CardId = card.CardId, EventType = CardEventType.Blocked,
            PerformedBy = performedBy, Reason = "Frozen by parent", OccurredAtUtc = now
        }, ct);
        await _audit.RecordAsync("Card.ParentFreeze", nameof(NfcCard), cardId.ToString(),
            new { card.UserId, card.CardUid }, ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
