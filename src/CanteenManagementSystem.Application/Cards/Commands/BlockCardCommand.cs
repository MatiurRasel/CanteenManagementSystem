// =============================================================================
// BlockCardCommand
// -----------------------------------------------------------------------------
// FLOW
//   1. Load card. Reject if missing or already blocked.
//   2. Set status=Blocked, stamp BlockedAtUtc.
//   3. CardEvent (Blocked) + AuditTrail.
//   4. Invalidate cache so the next swipe rejects immediately.
//
// USE CASES: lost card, fraud suspicion, expired enrolment.
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

public sealed record BlockCardCommand(BlockCardRequest Request) : ICommand<CardOperationResult>;

internal sealed class BlockCardCommandHandler : IRequestHandler<BlockCardCommand, CardOperationResult>
{
    private readonly IUnitOfWork _uow;
    private readonly ICacheService _cache;
    private readonly IAuditTrail _audit;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;

    public BlockCardCommandHandler(IUnitOfWork uow, ICacheService cache, IAuditTrail audit, ICurrentUser user, IClock clock)
    { _uow = uow; _cache = cache; _audit = audit; _user = user; _clock = clock; }

    public async Task<CardOperationResult> HandleAsync(BlockCardCommand command, CancellationToken cancellationToken)
    {
        var cards  = _uow.Repository<NfcCard>();
        var events = _uow.Repository<CardEvent>();

        var card = await cards.FirstOrDefaultAsync(c => c.CardId == command.Request.CardId, cancellationToken);
        if (card is null) return new CardOperationResult(false, "Card not found.");
        if (card.Status == CardStatus.Blocked) return new CardOperationResult(false, "Card is already blocked.");

        var previousUid = card.CardUid;
        card.Status = CardStatus.Blocked;
        card.BlockedAtUtc = _clock.UtcNow;

        await events.AddAsync(new CardEvent
        {
            CardId = card.CardId,
            EventType = CardEventType.Blocked,
            PreviousCardUid = previousUid,
            PerformedBy = _user.UserId,
            Reason = command.Request.Reason,
            OccurredAtUtc = _clock.UtcNow
        }, cancellationToken);

        await _audit.RecordAsync("Card.Blocked", nameof(NfcCard), card.CardId.ToString(),
            new { card.UserId, card.UserType, command.Request.Reason }, cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);
        await _cache.InvalidateAsync(string.Format(CacheKeys.CardByUid, previousUid), cancellationToken);
        return new CardOperationResult(true, "Card blocked.", card.CardId);
    }
}
