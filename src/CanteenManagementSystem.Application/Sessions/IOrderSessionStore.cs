// =============================================================================
// IOrderSessionStore  (CanteenManagementSystem.Application.Sessions)
// -----------------------------------------------------------------------------
// Storage abstraction for the short-lived OrderSession used by the keypad / NFC
// counter flow. Replaces the previous static-Dict in VerificationController
// (single-server only) with a tenant-aware distributed-cache backing so the
// same canteen can run multiple counter PCs behind a load balancer.
//
// SEMANTICS
//   * Each session lives keyed by SessionId for the configured timeout.
//   * Set is overwrite (last write wins) — the keypad flow ALWAYS reads then
//     writes, so the race window is bounded by one keystroke.
//   * Remove is idempotent (returning a missing key is not an error).
// =============================================================================

using CanteenManagementSystem.Domain.Sessions;

namespace CanteenManagementSystem.Application.Sessions;

public interface IOrderSessionStore
{
    Task<OrderSession?> GetAsync(string sessionId, CancellationToken cancellationToken = default);
    Task SetAsync(OrderSession session, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task RemoveAsync(string sessionId, CancellationToken cancellationToken = default);
}
