// =============================================================================
// CardEvent  (Domain.Cards)
// -----------------------------------------------------------------------------
// Immutable audit row written for every lifecycle change on an NfcCard.
// Source of truth for "who did what to this card and when". Critical for
// disputes and insurance claims on lost cards with attached balance.
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;

namespace CanteenManagementSystem.Domain.Cards;

[Table("CanteenCardEvents")]
public class CardEvent : ITenantOwned
{
    [Key]
    public long CardEventId { get; set; }

    public int CardId { get; set; }

    [ForeignKey(nameof(CardId))]
    public NfcCard? Card { get; set; }

    [Required]
    public CardEventType EventType { get; set; }

    [StringLength(64)]
    public string? PreviousCardUid { get; set; }

    [StringLength(64)]
    public string? NewCardUid { get; set; }

    [StringLength(100)]
    public string? PerformedBy { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
