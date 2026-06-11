// =============================================================================
// SignalROrderBroadcaster  (Presentation.RealTime)
// -----------------------------------------------------------------------------
// Implements IOrderBroadcaster by routing through the SignalR hub. Lives in
// Presentation because Infrastructure must stay AspNetCore-free.
// =============================================================================

using Platform.Application.Abstractions.RealTime;
using Platform.Domain.Tenancy;
using CanteenManagementSystem.Presentation.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CanteenManagementSystem.Presentation.RealTime;

internal sealed class SignalROrderBroadcaster : IOrderBroadcaster
{
    private readonly IHubContext<KitchenDisplayHub> _hub;
    private readonly ITenantContext _tenant;

    public SignalROrderBroadcaster(IHubContext<KitchenDisplayHub> hub, ITenantContext tenant)
    {
        _hub = hub; _tenant = tenant;
    }

    private string Group => string.IsNullOrEmpty(_tenant.ClientCode) ? "default" : _tenant.ClientCode;

    public Task OrderPlacedAsync(object payload, CancellationToken cancellationToken = default)
        => _hub.Clients.Group(Group).SendAsync("order:placed", payload, cancellationToken);

    public Task OrderStatusChangedAsync(object payload, CancellationToken cancellationToken = default)
        => _hub.Clients.Group(Group).SendAsync("order:status", payload, cancellationToken);

    public Task InventoryChangedAsync(object payload, CancellationToken cancellationToken = default)
        => _hub.Clients.Group(Group).SendAsync("inventory:changed", payload, cancellationToken);

    public Task BalanceChangedAsync(object payload, CancellationToken cancellationToken = default)
        => _hub.Clients.Group(Group).SendAsync("wallet:balance-changed", payload, cancellationToken);

    public Task StockOutAsync(object payload, CancellationToken cancellationToken = default)
        => _hub.Clients.Group(Group).SendAsync("inventory:stockout", payload, cancellationToken);

    public Task DirectorySyncCompletedAsync(object payload, CancellationToken cancellationToken = default)
        => _hub.Clients.Group(Group).SendAsync("directory:synced", payload, cancellationToken);
}
