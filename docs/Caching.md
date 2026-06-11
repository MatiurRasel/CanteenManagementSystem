# Caching

Three layers cooperate.

## L1 — IMemoryCache (per-instance, microsecond)

Process-local. Wiped on app restart. Used as a hot path so most reads never
touch Redis.

## L2 — IDistributedCache (Redis / in-memory fallback)

Cluster-safe. The `ConnectionStrings:Redis` setting flips between Redis and
the in-memory shim. Keys are tenant-prefixed by `CacheService` so two tenants
on the same Redis instance never collide.

## Output cache (ASP.NET Core, response-level)

`AddOutputCache` policies:

* **Default** — 10 s base policy, varies by query string.
* **DisplayMenu** — 15 s for the public board.
* **Reports** — 30 s, varies by query (date ranges).

### Behind a load balancer

When more than one replica serves traffic, the **in-process** output-cache
store is unsafe: the same request hits a different replica each time, and
each replica has its own cache copy. Symptoms: "phantom stale data,"
inconsistent dashboards, cache races on writes.

`Program.cs` auto-promotes the output-cache store to **Redis** when
`ConnectionStrings:Redis` is configured:

```csharp
if (!string.IsNullOrWhiteSpace(redisConn))
    outputCacheBuilder.AddRedisOutputCache(o => o.Configuration = redisConn);
```

Same pattern for SignalR — it joins a Redis backplane so hub events
broadcast to every connected client regardless of which replica produced
them.

### L1 (per-instance memory) staleness across replicas

The two-tier `CacheService` writes the value to **both** L1 and L2. When a
write hits replica A and calls `InvalidateAsync`, replica A's L1 is wiped
and L2 is wiped — but replica B's L1 still serves the stale value until
its TTL expires. We mitigate this with **deliberately short L1 TTLs**
(`CacheTtl.Short` = 30 s by default; hot keys like menu use 10 s). For
"must be consistent" data (price, balance, stock) we either skip L1 or
write through L2 directly.

If you need bullet-proof cross-replica L1 invalidation later, plug a
`StackExchange.Redis` pub/sub channel into `CacheService.InvalidateAsync`
and subscribe at startup to evict from local L1 on the published key.
The interface (`ICacheService`) stays the same.

## Tag-based invalidation

Tag a write to a logical bucket, then evict everything in that bucket:

```csharp
await cache.SetAsync("menu:today", value, ttl, tags: new[] { "menu" });
// later, after a menu mutation:
await cache.InvalidateTagAsync("menu");
```

Tag indices live in Redis as JSON arrays keyed by tag. Eviction wipes every
listed key from both L1 and L2.

## Key registry

All cache keys live in `CacheKeys.cs`. Update that file (and the table below)
whenever you add a new cached value.

| Key | TTL | Tags | Purpose |
|---|---|---|---|
| `tenant-settings:all` | Long (1h) | `tenant-settings` | Full settings bag for current tenant |
| `client:info` | Long | none | UTClientInfo row |
| `client:settings` | Long | none | UTClientSettings row |
| `menu:today` | Medium (5m) | `menu` | Today's menu DTO list |
| `user:profile:{userId}:{userType}` | Medium | none | User profile snapshot |
| `card:uid:{uid}` | Medium | none | Card resolution by UID |
| `reports:*:{from}:{to}` | Short (60s) | none | Aggregated reports |
| `payments:bkash:token` | 50 minutes | none | bKash id_token |

## Invalidation triggers

| Trigger | Tags to wipe |
|---|---|
| `TenantSettingsService.SetAsync` | `tenant-settings` |
| `MenuManagementService.*` (writes) | `menu` (todo: call) |
| `IssueCard/Block/Reassign` commands | `card:uid:{old}` + `card:uid:{new}` |
