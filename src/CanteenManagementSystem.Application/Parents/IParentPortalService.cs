// =============================================================================
// IParentPortalService  (CanteenManagementSystem.Application.Parents)
// -----------------------------------------------------------------------------
// Parent portal — dashboard + top-up + freeze-card. ADR 0004.
// =============================================================================

using CanteenManagementSystem.Domain.Cards;
using CanteenManagementSystem.Domain.Orders;
using Platform.Application.Results;

namespace CanteenManagementSystem.Application.Parents;

public sealed record ParentChildSnapshot(
    string ExternalId, string Name, string? Program, string? ContactNo,
    decimal Balance, decimal SpentThisMonth,
    int? ActiveCardId, string? ActiveCardUid, CardStatus? ActiveCardStatus,
    IReadOnlyList<Order> RecentOrders);

public interface IParentPortalService
{
    Task<IReadOnlyList<string>> GetLinkedChildIdsAsync(int userId, CancellationToken ct = default);
    Task<IReadOnlyList<ParentChildSnapshot>> BuildDashboardAsync(IReadOnlyList<string> linkedIds, CancellationToken ct = default);

    Task<Result> FreezeChildCardAsync(int cardId, IReadOnlyList<string> allowedExternalIds, string? performedBy, CancellationToken ct = default);
}
