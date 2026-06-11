// =============================================================================
// NfcCard  (Domain.Cards)
// -----------------------------------------------------------------------------
// One physical card / token bound to a user. The CardUid is the hardware
// identifier read by the NFC/RFID reader; UserId is the canonical canteen
// user. A card can be reassigned (CardUid changes, UserId stays) when a
// student loses their card and a fresh one is issued.
//
// INVARIANTS
//   - CardUid is unique across the tenant when status is Active.
//   - Blocked / Lost cards cannot place orders.
//   - At most one Active card per (UserId, UserType).
//
// AUDIT
//   Every lifecycle transition writes a CardEvent row (see CardEvent.cs).
// =============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Domain.Common;
using CanteenManagementSystem.Domain.Enums;

namespace CanteenManagementSystem.Domain.Cards;

[Table("CanteenNfcCards")]
public class NfcCard : IAggregateRoot, ITenantOwned
{
    [Key]
    public int CardId { get; set; }

    /// <summary>The hardware identifier scanned by the reader. Hex string typically.</summary>
    [Required, StringLength(64)]
    public string CardUid { get; set; } = string.Empty;

    /// <summary>Canteen-side user this card is bound to.</summary>
    [Required, StringLength(15)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public CanteenUserType UserType { get; set; }

    [Required]
    public CardStatus Status { get; set; } = CardStatus.Issued;

    [StringLength(50)]
    public string? IssuedBy { get; set; }

    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ActivatedAtUtc { get; set; }
    public DateTime? BlockedAtUtc { get; set; }
    public DateTime? RetiredAtUtc { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public byte[]? RowVersion { get; set; }
}
