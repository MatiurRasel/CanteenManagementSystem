// =============================================================================
// KitchenDisplayHub  (Presentation.Hubs)
// -----------------------------------------------------------------------------
// SignalR hub clients subscribe to for real-time order events.
//
// CONNECTION FLOW
//   1. Client opens WebSocket to /hubs/kitchen with ?tenant={clientCode}.
//   2. OnConnectedAsync places the connection into the SignalR group named
//      after the tenant code, so broadcasts stay tenant-isolated.
//   3. Server pushes events via IHubContext<KitchenDisplayHub>.Clients.Group(...).
// =============================================================================

using Platform.Domain.Tenancy;
using Microsoft.AspNetCore.SignalR;

namespace CanteenManagementSystem.Presentation.Hubs;

public sealed class KitchenDisplayHub : Hub
{
    private readonly ITenantContext _tenant;
    public KitchenDisplayHub(ITenantContext tenant) => _tenant = tenant;

    public override async Task OnConnectedAsync()
    {
        var group = string.IsNullOrEmpty(_tenant.ClientCode) ? "default" : _tenant.ClientCode;
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var group = string.IsNullOrEmpty(_tenant.ClientCode) ? "default" : _tenant.ClientCode;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
        await base.OnDisconnectedAsync(exception);
    }
}
