namespace CanteenManagementSystem.Domain.Cards;

/// <summary>
/// Lifecycle state for an NFC / RFID / QR identity card.
/// </summary>
/// <remarks>
/// FLOW:
///   Issued     -> Active (on first successful tap)
///   Active     -> Blocked (lost / stolen / fraud)
///   Blocked    -> Reassigned (operator wipes user binding + reissues)
///   Active     -> Retired  (graduation / employment ended)
/// </remarks>
public enum CardStatus
{
    Issued = 1,
    Active = 2,
    Blocked = 3,
    Reassigned = 4,
    Retired = 5,
    Lost = 6
}
