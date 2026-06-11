// =============================================================================
// ITenantDeletionService  (CanteenManagementSystem.Application.Tenancy)
// -----------------------------------------------------------------------------
// GDPR-grade tenant off-boarding.
//
//   SoftDeleteAsync(reason)  — flips Client.DeletedAtUtc + HoldUntilUtc.
//                              Tenant immediately fails the IsActive predicate
//                              so login/access stops. Data still on disk.
//   RestoreAsync()           — undo within the hold window.
//   HardDeleteAsync()        — irreversible cascade-delete of every
//                              ITenantOwned row + the Client row itself.
//                              Called by TenantHardDeleteService when
//                              HoldUntilUtc has passed.
//
// The default hold window is 30 days (Tenancy.HoldDays setting; min 7).
// =============================================================================

namespace CanteenManagementSystem.Application.Tenancy;

public sealed record SoftDeleteRequest(int ClientId, string Reason, int? HoldDaysOverride, string? PerformedBy);

public interface ITenantDeletionService
{
    Task<bool> SoftDeleteAsync(SoftDeleteRequest request, CancellationToken ct = default);
    Task<bool> RestoreAsync(int clientId, string? performedBy, CancellationToken ct = default);
    Task<int>  HardDeleteAsync(int clientId, string? performedBy, CancellationToken ct = default);
}
