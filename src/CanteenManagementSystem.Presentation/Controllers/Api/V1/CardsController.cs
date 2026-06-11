// =============================================================================
// CardsController (API v1)
// -----------------------------------------------------------------------------
// Per ADR 0004 — repos + IDispatcher + ICacheService only. No IAppDbContext.
// =============================================================================

using Asp.Versioning;
using CanteenManagementSystem.Application.Cards.Commands;
using CanteenManagementSystem.Application.Cards.Dtos;
using CanteenManagementSystem.Domain.Cards;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions.Caching;
using Platform.Application.Caching;
using Platform.Application.Dispatch;
using Platform.Application.Persistence;

namespace CanteenManagementSystem.Presentation.Controllers.Api.V1;

/// <summary>NFC / RFID card lifecycle: issue, reassign, block-lost, history.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/cards")]
[Produces("application/json")]
public sealed class CardsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly IReadOnlyRepository<NfcCard> _cards;
    private readonly IReadOnlyRepository<CardEvent> _events;
    private readonly ICacheService _cache;

    public CardsController(
        IDispatcher dispatcher,
        IReadOnlyRepository<NfcCard> cards,
        IReadOnlyRepository<CardEvent> events,
        ICacheService cache)
    {
        _dispatcher = dispatcher;
        _cards = cards;
        _events = events;
        _cache = cache;
    }

    /// <summary>Resolve a CardUid to its current binding (cached).</summary>
    [HttpGet("by-uid/{cardUid}")]
    public async Task<ActionResult<CardDto>> ByUid(string cardUid, CancellationToken cancellationToken)
    {
        var dto = await _cache.GetOrSetAsync(
            string.Format(CacheKeys.CardByUid, cardUid),
            async ct =>
            {
                var card = await _cards.FirstOrDefaultAsync(c => c.CardUid == cardUid, ct);
                return card is null ? null : new CardDto(
                    card.CardId, card.CardUid, card.UserId, card.UserType, card.Status,
                    card.IssuedAtUtc, card.ActivatedAtUtc, card.BlockedAtUtc, card.Notes);
            },
            ttl: CacheTtl.Medium,
            tags: null,
            cancellationToken: cancellationToken);

        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Issue a new card to a user.</summary>
    [HttpPost("issue")]
    public async Task<ActionResult<CardOperationResult>> Issue([FromBody] IssueCardRequest request, CancellationToken cancellationToken)
        => Ok(await _dispatcher.SendAsync(new IssueCardCommand(request), cancellationToken));

    /// <summary>Block an existing card (lost / fraud / retirement).</summary>
    [HttpPost("block")]
    public async Task<ActionResult<CardOperationResult>> Block([FromBody] BlockCardRequest request, CancellationToken cancellationToken)
        => Ok(await _dispatcher.SendAsync(new BlockCardCommand(request), cancellationToken));

    /// <summary>Reassign a card UID without changing the user binding.</summary>
    [HttpPost("reassign")]
    public async Task<ActionResult<CardOperationResult>> Reassign([FromBody] ReassignCardRequest request, CancellationToken cancellationToken)
        => Ok(await _dispatcher.SendAsync(new ReassignCardCommand(request), cancellationToken));

    /// <summary>Full lifecycle history for a card.</summary>
    [HttpGet("{cardId:int}/history")]
    public async Task<ActionResult<IReadOnlyCollection<CardEvent>>> History(int cardId, CancellationToken cancellationToken)
        => Ok(await _events.NoTrackingQuery()
            .Where(e => e.CardId == cardId)
            .OrderByDescending(e => e.OccurredAtUtc)
            .ToListAsync(cancellationToken));
}
