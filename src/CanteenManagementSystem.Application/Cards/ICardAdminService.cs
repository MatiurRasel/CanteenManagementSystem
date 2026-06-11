// =============================================================================
// ICardAdminService  (CanteenManagementSystem.Application.Cards)
// -----------------------------------------------------------------------------
// Admin-side lifecycle management for NfcCard. Used by /admin/cards so the
// controller never touches IAppDbContext directly (ADR 0004).
//
// Each lifecycle method writes a CardEvent timeline row and an AuditEntry
// in the same transaction as the status flip.
// =============================================================================

using CanteenManagementSystem.Domain.Cards;
using CanteenManagementSystem.Domain.Enums;
using Platform.Application.Results;

namespace CanteenManagementSystem.Application.Cards;

public sealed record CardListItem(
    int CardId, string CardUid, string UserId, CanteenUserType UserType,
    CardStatus Status, DateTime IssuedAtUtc, DateTime? BlockedAtUtc);

public sealed record CardDetail(NfcCard Card, IReadOnlyList<CardEvent> Events);

public sealed record CardIssueInput(string CardUid, string UserId, CanteenUserType UserType, string? Notes);

public sealed record CardReassignInput(string NewCardUid, string? Reason);

public interface ICardAdminService
{
    Task<IReadOnlyList<CardListItem>> ListAsync(string? search, CardStatus? status, CancellationToken ct = default);
    Task<CardDetail?> GetDetailAsync(int cardId, CancellationToken ct = default);

    Task<Result<NfcCard>> IssueAsync(CardIssueInput input, string? performedBy, CancellationToken ct = default);
    Task<Result<NfcCard>> ReassignAsync(int cardId, CardReassignInput input, string? performedBy, CancellationToken ct = default);

    Task<Result<NfcCard>> BlockAsync(int cardId, string? reason, string? performedBy, CancellationToken ct = default);
    Task<Result<NfcCard>> UnblockAsync(int cardId, string? reason, string? performedBy, CancellationToken ct = default);
    Task<Result<NfcCard>> ReportLostAsync(int cardId, string? reason, string? performedBy, CancellationToken ct = default);
    Task<Result<NfcCard>> ActivateAsync(int cardId, string? performedBy, CancellationToken ct = default);
    Task<Result<NfcCard>> RetireAsync(int cardId, string? reason, string? performedBy, CancellationToken ct = default);
}
