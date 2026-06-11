// =============================================================================
// IssueCardCommand
// -----------------------------------------------------------------------------
// FLOW
//   1. Validate: CardUid unique, user exists, no other Active card for user.
//   2. Insert NfcCard with status=Issued.
//   3. Write CardEvent row (Issued).
//   4. Bust the "card:uid:{uid}" cache so the very next swipe sees the new
//      card (no waiting for TTL expiry).
//   5. Return CardOperationResult.
//
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
using FluentValidation;

namespace CanteenManagementSystem.Application.Cards.Commands;

public sealed record IssueCardCommand(IssueCardRequest Request) : ICommand<CardOperationResult>;

public sealed class IssueCardCommandValidator : AbstractValidator<IssueCardCommand>
{
    public IssueCardCommandValidator()
    {
        RuleFor(c => c.Request.CardUid).NotEmpty().MaximumLength(64);
        RuleFor(c => c.Request.UserId).NotEmpty().MaximumLength(15);
    }
}

internal sealed class IssueCardCommandHandler : IRequestHandler<IssueCardCommand, CardOperationResult>
{
    private readonly IUnitOfWork _uow;
    private readonly ICacheService _cache;
    private readonly IAuditTrail _audit;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;

    public IssueCardCommandHandler(IUnitOfWork uow, ICacheService cache, IAuditTrail audit, ICurrentUser user, IClock clock)
    {
        _uow = uow; _cache = cache; _audit = audit; _user = user; _clock = clock;
    }

    public async Task<CardOperationResult> HandleAsync(IssueCardCommand command, CancellationToken cancellationToken)
    {
        var req = command.Request;
        var cards  = _uow.Repository<NfcCard>();
        var events = _uow.Repository<CardEvent>();

        // Uniqueness check on card uid for the tenant.
        var existingByUid = await cards.AnyAsync(
            c => c.CardUid == req.CardUid && c.Status == CardStatus.Active, cancellationToken);
        if (existingByUid) return new CardOperationResult(false, "Card UID is already active.");

        // Block dual-active cards for one user.
        var hasActive = await cards.AnyAsync(
            c => c.UserId == req.UserId && c.UserType == req.UserType && c.Status == CardStatus.Active, cancellationToken);
        if (hasActive) return new CardOperationResult(false, "User already has an active card. Reassign instead.");

        var card = new NfcCard
        {
            CardUid = req.CardUid,
            UserId = req.UserId,
            UserType = req.UserType,
            Status = CardStatus.Issued,
            IssuedBy = _user.UserId,
            IssuedAtUtc = _clock.UtcNow,
            Notes = req.Notes
        };
        await cards.AddAsync(card, cancellationToken);

        await events.AddAsync(new CardEvent
        {
            Card = card,
            EventType = CardEventType.Issued,
            NewCardUid = req.CardUid,
            PerformedBy = _user.UserId,
            Reason = req.Notes,
            OccurredAtUtc = _clock.UtcNow
        }, cancellationToken);

        await _audit.RecordAsync("Card.Issued", nameof(NfcCard), req.CardUid,
            new { req.UserId, req.UserType }, cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);

        await _cache.InvalidateAsync(string.Format(CacheKeys.CardByUid, req.CardUid), cancellationToken);
        return new CardOperationResult(true, "Card issued.", card.CardId);
    }
}
