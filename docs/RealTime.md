# Real-Time

## Hub

`KitchenDisplayHub` lives at `/hubs/kitchen`. Connections are grouped by
tenant (`ITenantContext.ClientCode`) so cross-tenant fan-out is impossible.

## Events

| Event name | Payload | Pushed when |
|---|---|---|
| `order:placed` | order summary | After `PlaceOrderCommandHandler` succeeds |
| `order:status` | `{ orderId, fromStatus, toStatus, at }` | After any status-change command |
| `inventory:changed` | `{ dailyMenuId, availableQuantity }` | After `InventoryService.ReserveAsync`/`ConsumeAsync` |

## Client subscription (jQuery-friendly)

```html
<script src="https://cdn.jsdelivr.net/npm/@microsoft/signalr/dist/browser/signalr.min.js"></script>
<script>
  const conn = new signalR.HubConnectionBuilder().withUrl("/hubs/kitchen").build();
  conn.on("order:placed",      (o) => console.log("Placed", o));
  conn.on("order:status",      (s) => console.log("Status", s));
  conn.on("inventory:changed", (i) => console.log("Stock", i));
  await conn.start();
</script>
```

## Why an Application-layer abstraction

Command handlers depend on `IOrderBroadcaster`, not on `IHubContext<>`. That
keeps Application free of `Microsoft.AspNetCore.SignalR`. The Presentation
layer registers `SignalROrderBroadcaster` as the concrete impl.

## Backplane for scale-out

When running multiple instances, add a Redis backplane:

```csharp
builder.Services.AddSignalR().AddStackExchangeRedis(redisConn);
```

Group fan-out then works across instances.
