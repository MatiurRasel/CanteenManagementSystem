// =============================================================================
// IOrderBroadcaster  (Application abstraction)
// -----------------------------------------------------------------------------
// Thin seam over SignalR so command handlers can push live events without
// taking a dependency on ASP.NET Core. The Infrastructure impl wraps an
// IHubContext<KitchenDisplayHub> and fan-outs to all subscribers of the
// tenant group.
//
// EVENTS PUSHED
//   OrderPlaced       -> { orderId, orderNumber, total, items[] }
//   OrderStatusChanged-> { orderId, fromStatus, toStatus, at }
//   InventoryChanged  -> { dailyMenuId, availableQuantity }
//
// FLOW (frontend)
//   The /Display/Menu and /Operator/Dashboard pages connect to /hubs/kitchen
//   via @microsoft/signalr (or our jQuery-friendly @aspnet/signalr v1) and
//   subscribe to "order:placed", "order:status", "inventory:changed".
// =============================================================================

namespace Platform.Application.Abstractions.RealTime;

public interface IOrderBroadcaster
{
    Task OrderPlacedAsync(object payload, CancellationToken cancellationToken = default);
    Task OrderStatusChangedAsync(object payload, CancellationToken cancellationToken = default);
    Task InventoryChangedAsync(object payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Push a "wallet balance changed" event so the counter UI can refresh the
    /// available-balance badge live (Block / Release / Deduct / Recharge / Refund).
    /// Payload shape: { userId, userType, total, used, blocked, available }.
    /// </summary>
    Task BalanceChangedAsync(object payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Push a "stock-out" event when a menu item's available quantity drops to zero
    /// so the counter UI can grey-out the keypad number immediately.
    /// Payload shape: { dailyMenuId, foodItemId, itemName }.
    /// </summary>
    Task StockOutAsync(object payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Push a "directory sync finished" event so the /directory-admin page can
    /// flip from "Sync now" to a fresh health card without a manual refresh.
    /// Payload shape: { runId, status, source, students Δ, employees Δ, durationSec, errorMessage? }.
    /// </summary>
    Task DirectorySyncCompletedAsync(object payload, CancellationToken cancellationToken = default);
}
