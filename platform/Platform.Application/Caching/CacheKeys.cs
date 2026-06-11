// =============================================================================
// CacheKeys & CacheTtl
// -----------------------------------------------------------------------------
// One authoritative registry of every cache key + TTL used in the system. We
// keep this file tiny on purpose so a code reviewer can see, at a glance:
//   - Every piece of data we cache
//   - For how long
//   - With what tenant scope
//
// CONVENTIONS
//   - Keys are *colon-delimited* and *lowercase* segments.
//   - Tenant-scoped keys are produced by ITenantCacheKeyBuilder which
//     prepends "tenant:{code}:" — never hand-build cross-tenant keys.
//   - Update this file *and* the README docs/Caching.md whenever you add a
//     new cache entry so observability stays clean.
// =============================================================================

namespace Platform.Application.Caching;

public static class CacheKeys
{
    // Tenant configuration ---------------------------------------------------
    public const string TenantSettingsAll = "tenant-settings:all";
    public const string TenantSettingsTag = "tenant-settings";

    // Current tenant (Client row + cached Azure photo container path) -------
    public const string ClientInfo     = "client:current";
    public const string PhotoContainer = "client:photo-container";

    // Menu -------------------------------------------------------------------
    public const string TodayMenu      = "menu:today";          // value: List<MenuItemDto>
    public const string MenuByDate     = "menu:by-date:{0:yyyy-MM-dd}";

    // Users ------------------------------------------------------------------
    public const string UserProfile    = "user:profile:{0}:{1}";  // userId, userType

    // Reports (cache for 60s while the chart auto-refreshes) -----------------
    public const string DashboardKpi   = "reports:kpi:{0:yyyy-MM-dd}";
    public const string PopularItems   = "reports:popular:{0:yyyy-MM-dd}";

    // NFC cards --------------------------------------------------------------
    public const string CardByUid      = "card:uid:{0}";
}

public static class CacheTtl
{
    /// <summary>Short cache for hot endpoints that change frequently (5 seconds).</summary>
    public static readonly TimeSpan Hot   = TimeSpan.FromSeconds(5);

    /// <summary>Default cache for read-mostly data (60 seconds).</summary>
    public static readonly TimeSpan Short = TimeSpan.FromMinutes(1);

    /// <summary>Medium cache for slowly-changing data like today's menu (5 minutes).</summary>
    public static readonly TimeSpan Medium = TimeSpan.FromMinutes(5);

    /// <summary>Long cache for tenant config + client metadata (1 hour). Invalidated explicitly on write.</summary>
    public static readonly TimeSpan Long   = TimeSpan.FromHours(1);

    /// <summary>Very long cache for static reference data (24 hours).</summary>
    public static readonly TimeSpan Day    = TimeSpan.FromHours(24);
}
