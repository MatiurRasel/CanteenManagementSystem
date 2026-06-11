// =============================================================================
// ReassignCardCommand
// -----------------------------------------------------------------------------
// FLOW
//   1. Load existing card by id.
//   2. Verify the new CardUid is not used by another active card.
//   3. Swap CardUid, set status=Active.
//   4. CardEvent (Reassigned) records old + new UIDs.
//   5. Bust cache for both old and new UIDs.
//
// USE CASES: student lost the physical card and operator issued them a new
// blank token without changing their canteen balance.
// ADR 0004: data access via IUnitOfWork — no IAppDbContext.
// =============================================================================

using Platform.Application.Persistence;
using CanteenManagementSystem.Application.Cards.Dtos;
using Platform.Application.Abstractions.Audit;
using Platform.Application.Abstractions.Caching;
using Platform.Application.Abstractions.Identity;
using Platform.Application.Abstractions.Time;
using Platform.Application.Caching;
using Platform.Application.Dispatch;
using CanteenManagementSystem.Domain.Cards;

namespace CanteenManagementSystem.Application.Cards.Commands;

public sealed record ReassignCardCommand(ReassignCardRequest Request) : ICommand<CardOperationResult>;

internal sealed class ReassignCardCommandHandler : IRequestHandler<ReassignCardCommand, CardOperationResult>
{
    private readonly IUnitOfWork _uow;
    private readonly ICacheService _cache;
    private readonly IAuditTrail _audit;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;

    public ReassignCardCommandHandler(IUnitOfWork uow, ICacheService cache, IAuditTrail audit, ICurrentUser user, IClock clock)
    { _uow = uow; _cache = cache; _audit = audit; _user = user; _clock = clock; }

    public async Task<CardOperationResult> HandleAsync(ReassignCardCommand command, CancellationToken cancellationToken)
    {
        var cards  = _uow.Repository<NfcCard>();
        var events = _uow.Repository<CardEvent>();

        var card = await cards.FirstOrDefaultAsync(c => c.CardId == command.Request.CardId, cancellationToken);
        if (card is null) return new CardOperationResult(false, "Card not found.");

        var conflict = await cards.AnyAsync(
            c => c.CardUid == command.Request.NewCardUid && c.Status == CardStatus.Active, cancellationToken);
        if (conflict) return new CardOperationResult(false, "New card UID already active.");

        var previousUid = card.CardUid;
        card.CardUid = command.Request.NewCardUid;
        card.Status = CardStatus.Active;
        card.ActivatedAtUtc = _clock.UtcNow;
        card.BlockedAtUtc = null;

        await events.AddAsync(new CardEvent
        {
            CardId = card.CardId,
            EventType = CardEventType.Reassigned,
            PreviousCardUid = previousUid,
            NewCardUid = command.Request.NewCardUid,
            PerformedBy = _user.UserId,
            Reason = command.Request.Reason,
            OccurredAtUtc = _clock.UtcNow
        }, cancellationToken);

        await _audit.RecordAsync("Card.Reassigned", nameof(NfcCard), card.CardId.ToString(),
            new { previousUid, newUid = command.Request.NewCardUid }, cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);
        await _cache.InvalidateAsync(string.Format(CacheKeys.CardByUid, previousUid), cancellationToken);
        await _cache.InvalidateAsync(string.Format(CacheKeys.CardByUid, command.Request.NewCardUid), cancellationToken);
        return new CardOperationResult(true, "Card reassigned.", card.CardId);
    }
}
