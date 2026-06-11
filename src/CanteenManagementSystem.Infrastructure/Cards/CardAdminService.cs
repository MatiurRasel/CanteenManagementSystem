// =============================================================================
// CardAdminService  (CanteenManagementSystem.Infrastructure.Cards)
// -----------------------------------------------------------------------------
// ICardAdminService impl. ADR 0004 — uses IUnitOfWork + IAuditTrail; no
// IAppDbContext. Every lifecycle transition writes a CardEvent + AuditEntry
// in one transaction.
// =============================================================================

using CanteenManagementSystem.Application.Cards;
using CanteenManagementSystem.Domain.Cards;
using CanteenManagementSystem.Domain.Enums;
using Platform.Application.Abstractions.Audit;
using Platform.Application.Persistence;
using Platform.Application.Results;

namespace CanteenManagementSystem.Infrastructure.Cards;

public sealed class CardAdminService : ICardAdminService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditTrail _audit;

    public CardAdminService(IUnitOfWork uow, IAuditTrail audit)
    {
        _uow = uow; _audit = audit;
    }

    private IRepository<NfcCard>   Cards  => _uow.Repository<NfcCard>();
    private IRepository<CardEvent> Events => _uow.Repository<CardEvent>();

    public async Task<IReadOnlyList<CardListItem>> ListAsync(string? search, CardStatus? status, CancellationToken ct = default)
    {
        var q = Cards.NoTrackingQuery();
        if (status.HasValue) q = q.Where(c => c.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim();
            q = q.Where(c => c.CardUid.Contains(needle) || c.UserId.Contains(needle));
        }
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            q.OrderByDescending(c => c.IssuedAtUtc)
             .Take(500)
             .Select(c => new CardListItem(c.CardId, c.CardUid, c.UserId, c.UserType, c.Status, c.IssuedAtUtc, c.BlockedAtUtc)),
            ct);
    }

    public async Task<CardDetail?> GetDetailAsync(int cardId, CancellationToken ct = default)
    {
        var card = await Cards.FirstOrDefaultAsync(c => c.CardId == cardId, ct);
        if (card is null) return null;
        var events = await Events.ListAsync(e => e.CardId == cardId, ct);
        return new CardDetail(card, events.OrderByDescending(e => e.OccurredAtUtc).ToList());
    }

    public async Task<Result<NfcCard>> IssueAsync(CardIssueInput input, string? performedBy, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.CardUid) || string.IsNullOrWhiteSpace(input.UserId))
            return Result.Failure<NfcCard>(Error.Validation("Card UID and User ID are required."));

        var exists = await Cards.AnyAsync(c => c.CardUid == input.CardUid && c.Status == CardStatus.Active, ct);
        if (exists) return Result.Failure<NfcCard>(Error.Conflict($"Card UID {input.CardUid} is already active on another user."));

        var card = new NfcCard
        {
            CardUid    = input.CardUid.Trim(),
            UserId     = input.UserId.Trim(),
            UserType   = input.UserType,
            Status     = CardStatus.Issued,
            Notes      = input.Notes,
            IssuedBy   = performedBy,
            IssuedAtUtc = DateTime.UtcNow,
        };
        await Cards.AddAsync(card, ct);
        await _uow.SaveChangesAsync(ct);

        await Events.AddAsync(new CardEvent
        {
            CardId = card.CardId, EventType = CardEventType.Issued,
            NewCardUid = card.CardUid, PerformedBy = performedBy,
            Reason = "Initial issue", OccurredAtUtc = DateTime.UtcNow
        }, ct);
        await _uow.SaveChangesAsync(ct);

        await _audit.RecordAsync("Card.Issued", nameof(NfcCard), card.CardId.ToString(),
            new { card.CardUid, card.UserId }, ct);

        return Result.Success(card);
    }

    public async Task<Result<NfcCard>> ReassignAsync(int cardId, CardReassignInput input, string? performedBy, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.NewCardUid))
            return Result.Failure<NfcCard>(Error.Validation("New card UID is required."));

        var card = await Cards.FirstOrDefaultAsync(c => c.CardId == cardId, ct);
        if (card is null) return Result.Failure<NfcCard>(Error.NotFound("Card not found."));

        var dup = await Cards.AnyAsync(c => c.CardUid == input.NewCardUid && c.Status == CardStatus.Active && c.CardId != cardId, ct);
        if (dup) return Result.Failure<NfcCard>(Error.Conflict($"New UID {input.NewCardUid} is active on another card."));

        var previousUid = card.CardUid;
        card.CardUid = input.NewCardUid.Trim();
        card.Status = CardStatus.Active;
        card.BlockedAtUtc = null;
        card.ActivatedAtUtc ??= DateTime.UtcNow;
        Cards.Update(card);

        await Events.AddAsync(new CardEvent
        {
            CardId = card.CardId, EventType = CardEventType.Reassigned,
            PreviousCardUid = previousUid, NewCardUid = card.CardUid,
            PerformedBy = performedBy,
            Reason = input.Reason ?? "Re-issue", OccurredAtUtc = DateTime.UtcNow
        }, ct);
        await _uow.SaveChangesAsync(ct);

        await _audit.RecordAsync("Card.Reassigned", nameof(NfcCard), cardId.ToString(),
            new { previousUid, newUid = card.CardUid, input.Reason }, ct);
        return Result.Success(card);
    }

    public Task<Result<NfcCard>> BlockAsync(int cardId, string? reason, string? performedBy, CancellationToken ct = default)
        => TransitionAsync(cardId, CardStatus.Blocked, CardEventType.Blocked, reason ?? "Blocked by admin", performedBy,
            (c, now) => c.BlockedAtUtc = now, ct);

    public Task<Result<NfcCard>> UnblockAsync(int cardId, string? reason, string? performedBy, CancellationToken ct = default)
        => TransitionAsync(cardId, CardStatus.Active, CardEventType.Unblocked, reason ?? "Unblocked by admin", performedBy,
            (c, _) => c.BlockedAtUtc = null, ct);

    public Task<Result<NfcCard>> ReportLostAsync(int cardId, string? reason, string? performedBy, CancellationToken ct = default)
        => TransitionAsync(cardId, CardStatus.Lost, CardEventType.ReportedLost, reason ?? "Reported lost", performedBy,
            (c, now) => c.BlockedAtUtc = now, ct);

    public Task<Result<NfcCard>> ActivateAsync(int cardId, string? performedBy, CancellationToken ct = default)
        => TransitionAsync(cardId, CardStatus.Active, CardEventType.Activated, "Activated", performedBy,
            (c, now) => c.ActivatedAtUtc ??= now, ct);

    public Task<Result<NfcCard>> RetireAsync(int cardId, string? reason, string? performedBy, CancellationToken ct = default)
        => TransitionAsync(cardId, CardStatus.Retired, CardEventType.Retired, reason ?? "Retired", performedBy,
            (c, now) => c.RetiredAtUtc = now, ct);

    private async Task<Result<NfcCard>> TransitionAsync(int cardId, CardStatus next, CardEventType eventType,
        string reason, string? performedBy, Action<NfcCard, DateTime> mutate, CancellationToken ct)
    {
        var card = await Cards.FirstOrDefaultAsync(c => c.CardId == cardId, ct);
        if (card is null) return Result.Failure<NfcCard>(Error.NotFound("Card not found."));

        var now = DateTime.UtcNow;
        card.Status = next;
        mutate(card, now);
        Cards.Update(card);

        await Events.AddAsync(new CardEvent
        {
            CardId = card.CardId, EventType = eventType,
            PerformedBy = performedBy, Reason = reason, OccurredAtUtc = now
        }, ct);
        await _uow.SaveChangesAsync(ct);

        await _audit.RecordAsync($"Card.{eventType}", nameof(NfcCard), card.CardId.ToString(),
            new { card.CardUid, card.UserId, reason }, ct);
        return Result.Success(card);
    }
}
