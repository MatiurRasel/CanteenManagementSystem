// =============================================================================
// Program.cs  (Presentation entry point)
// -----------------------------------------------------------------------------
// Boot order (each line is a tiny composable seam):
//   1. Serilog bootstrap logger        → captures startup-time errors
//   2. AddCanteenApplication           → CQRS dispatcher, behaviors, options
//   3. AddCanteenInfrastructure        → DbContext, cache, wallet, payments,
//                                        notifications, telemetry deps
//   4. AddCanteenPresentation          → MVC, JSON, sessions, tenant chrome
//   5. AddCanteenOpenApi               → Swashbuckle + ApiVersioning
//   6. AddCanteenTelemetry             → OpenTelemetry traces + metrics
//   7. AddOutputCache                  → response caching for read endpoints
//   8. AddSignalR                      → KitchenDisplayHub
//
// HTTP pipeline (top-down):
//   * UseSerilogRequestLogging  - one log line per request
//   * UseMiddleware<GlobalExceptionMiddleware>   - RFC 7807 ProblemDetails
//   * UseHsts in production
//   * UseResponseCompression / UseHttpsRedirection / UseStaticFiles
//   * UseOutputCache       - short-circuits matching GETs from cache
//   * UseRequestLocalization
//   * UseRouting -> UseMiddleware<TenantContextMiddleware> -> UseSession/Auth
//   * MapControllers + MapHub<KitchenDisplayHub>
// =============================================================================

using CanteenManagementSystem.Application;
using Platform.Application.Abstractions.RealTime;
using CanteenManagementSystem.Infrastructure;
using CanteenManagementSystem.Presentation;
using Platform.Presentation.Api;
using Platform.Presentation.Auth;
using Platform.Presentation.Reporting;
using Platform.Presentation.Seeding;
using CanteenManagementSystem.Presentation.Hubs;
using Platform.Presentation.Middleware;
using Platform.Presentation.Observability;
using CanteenManagementSystem.Presentation.RealTime;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using Serilog;
using CanteenManagementSystem.Presentation.Observability;

// Bootstrap logger captures errors that fire BEFORE the host is up (DI not yet
// available). Configured fresh from appsettings.json once UseSerilog runs below.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithThreadId()
    .WriteTo.Console(
        outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Canteen Management System");

    var builder = WebApplication.CreateBuilder(args);

    // Host logger — configuration-driven so devops can change sinks / levels
    // without a redeploy. The base wiring below is the FALLBACK chain (Console
    // + rolling file + Seq), used when appsettings.json's "Serilog:WriteTo"
    // block is missing. Enrichers always run.
    builder.Host.UseSerilog((context, services, configuration) =>
    {
        var seqUrl = context.Configuration["Serilog:Seq:ServerUrl"];
        var seqKey = context.Configuration["Serilog:Seq:ApiKey"];

        configuration
            .ReadFrom.Configuration(context.Configuration)     // honours Serilog:WriteTo if present
            .ReadFrom.Services(services)                       // pulls registered ILogEventEnricher s
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithThreadId()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("Application", "CanteenManagementSystem")
            // Console: human-friendly during dev, JSON when ASPNETCORE_ENVIRONMENT=Production.
            // The dev template prefixes every line with `[tenant|correlation]` when set,
            // so a request log looks like:
            //   [12:23:01 INF] [ccpc|f3a9b2c1] HTTP GET /admin/dashboard responded 200 in 142.3 ms
            .WriteTo.Logger(lc =>
            {
                if (context.HostingEnvironment.IsProduction())
                {
                    lc.WriteTo.Console(new Serilog.Formatting.Compact.CompactJsonFormatter());
                }
                else
                {
                    lc.WriteTo.Console(
                        outputTemplate:
                            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
                }
            })
            // Rolling JSON file — machine-parseable, daily roll, 14-day retention.
            .WriteTo.File(
                formatter: new Serilog.Formatting.Compact.CompactJsonFormatter(),
                path: "logs/canteen-.json",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true);

        // Seq sink — opt-in. Set Serilog:Seq:ServerUrl in appsettings or env
        // (`SERILOG__SEQ__SERVERURL=http://localhost:5341`) to enable. Without
        // a URL, Seq stays silent — no startup crash, no noisy "connection
        // refused" loops.
        if (!string.IsNullOrWhiteSpace(seqUrl))
        {
            configuration.WriteTo.Seq(seqUrl, apiKey: string.IsNullOrWhiteSpace(seqKey) ? null : seqKey);
        }
    });

    // Enrichers resolved via DI — Serilog calls them through ReadFrom.Services.
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddSingleton<Serilog.Core.ILogEventEnricher, TenantLogEnricher>();

    builder.Services
        .AddCanteenApplication(builder.Configuration)
        .AddCanteenInfrastructure(builder.Configuration);

    // Platform-level cross-cutting wiring (auth + seed + reporting). Independent
    // of any single product, so each product opts-in once here.
    builder.Services.AddPlatformAuth(builder.Configuration);
    builder.Services.AddPlatformSeeding(builder.Configuration);
    builder.Services.AddPlatformReporting();

    builder.Services.AddCanteenPresentation();
    builder.Services.AddCanteenOpenApi();
    builder.Services.AddCanteenTelemetry(builder.Configuration);

    // Real-time broadcaster lives in Presentation because Infrastructure stays
    // ASP.NET-Core-free. Anyone that needs to push events depends on the
    // Application abstraction IOrderBroadcaster.
    //
    // Redis BACKPLANE — when ConnectionStrings:Redis is configured, SignalR
    // routes hub messages through Redis so multiple app instances see the
    // same broadcast. Without this, scaling out to >1 replica silently splits
    // viewers across the cluster. Falls back to in-process when Redis is
    // unconfigured (single-host dev / single-instance deployments).
    var signalRBuilder = builder.Services.AddSignalR();
    var redisConn = builder.Configuration.GetConnectionString("Redis");
    if (!string.IsNullOrWhiteSpace(redisConn))
    {
        signalRBuilder.AddStackExchangeRedis(redisConn, opts =>
        {
            opts.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("canteen-signalr:");
        });
    }
    builder.Services.AddScoped<IOrderBroadcaster, SignalROrderBroadcaster>();

    // ─── Health checks ────────────────────────────────────────────────────
    //   /health/live  → liveness (process up; never depends on DB)
    //   /health/ready → readiness (DB + directory subsystem ok)
    builder.Services
        .AddHealthChecks()
        .AddDbContextCheck<CanteenManagementSystem.Infrastructure.Persistence.ApplicationDbContext>(
            name: "db", tags: new[] { "ready" })
        .AddCheck<CanteenManagementSystem.Presentation.HealthChecks.DirectoryHealthCheck>(
            name: "directory", tags: new[] { "ready" });

    // ─── Rate limiting ────────────────────────────────────────────────────
    //   "auth-login"   — 5 attempts / minute per IP on /Account/Login + the
    //                    JSON /api/v1/auth/login. Hard cap protects against
    //                    credential-stuffing while still letting humans recover.
    //   Global limit   — 200 requests / 30 s per IP across the whole app,
    //                    rejecting with 429 + Retry-After header.
    builder.Services.AddRateLimiter(opts =>
    {
        opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        opts.OnRejected = async (ctx, ct) =>
        {
            ctx.HttpContext.Response.Headers.RetryAfter = "60";
            await ctx.HttpContext.Response.WriteAsync("Too many requests — try again shortly.", ct);
        };

        opts.AddPolicy("auth-login", httpContext =>
            System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
                factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window      = TimeSpan.FromMinutes(1),
                    QueueLimit  = 0,
                    QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst
                }));

        opts.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
                factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                {
                    PermitLimit = 200,
                    Window      = TimeSpan.FromSeconds(30),
                    QueueLimit  = 0,
                    QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst
                }));
    });

    // ─── Response/Output cache ────────────────────────────────────────────
    // BEHIND A LOAD BALANCER (multi-replica): the in-process OutputCache
    // store sees a different cached payload per node — same request hits
    // 3 replicas, gets 3 different copies. The fix is a distributed
    // IOutputCacheStore. For now we deliberately keep the in-process store
    // and DOCUMENT the swap path in docs/Caching.md so deployers wire it
    // up at scale-out time:
    //
    //   1. Install Microsoft.AspNetCore.OutputCaching.StackExchangeRedis
    //      (preview as of .NET 10).
    //   2. After AddOutputCache(...), call:
    //        services.AddRedisOutputCacheStore(o => o.Configuration = redisConn);
    //   3. Same Redis instance used by ICacheService L2 + SignalR backplane.
    //
    // ICacheService (L1+L2 via IDistributedCache) already covers cluster
    // safety for the hot domain caches — OutputCache is response-level
    // only and the loss-blast is bounded by the 10-30s TTLs below.
    // ResponseCaching middleware services — required by the [ResponseCache
    // (VaryByQueryKeys = ...)] attribute on PublicMenuController (GET /menu).
    // Without UseResponseCaching the attribute throws InvalidOperationException
    // at request time ("'VaryByQueryKeys' requires the response cache middleware").
    builder.Services.AddResponseCaching();

    builder.Services.AddOutputCache(options =>
    {
        // Default policy: 10 seconds, varies by query string and culture.
        options.AddBasePolicy(b => b.Expire(TimeSpan.FromSeconds(10)));
        // Public display board: 15s, vary by query.
        options.AddPolicy("DisplayMenu", b => b.Expire(TimeSpan.FromSeconds(15)).SetVaryByQuery("*"));
        // Reports: 30s, vary by query string (date ranges).
        options.AddPolicy("Reports",     b => b.Expire(TimeSpan.FromSeconds(30)).SetVaryByQuery("*"));
    });

    var app = builder.Build();

    // Run migrations + seeds before serving traffic. Both toggles are
    // independent — see "Migration" and "Seed" keys in appsettings.json.
    await app.RunPlatformSeedAsync();

    // Correlation-ID FIRST so every log line in the rest of the pipeline
    // (including the request-summary line below) carries the same id.
    app.UseMiddleware<CanteenManagementSystem.Presentation.Observability.CorrelationIdMiddleware>();

    app.UseSerilogRequestLogging(opts =>
    {
        opts.EnrichDiagnosticContext = (diag, ctx) =>
        {
            diag.Set("RequestHost",   ctx.Request.Host.Value ?? string.Empty);
            diag.Set("RequestScheme", ctx.Request.Scheme);
            diag.Set("UserAgent",     ctx.Request.Headers.UserAgent.ToString());
            if (ctx.User?.Identity?.IsAuthenticated == true)
            {
                diag.Set("User", ctx.User.Identity.Name ?? "(?)");
            }
            if (ctx.Items.TryGetValue("CorrelationId", out var cid) && cid is string s)
            {
                diag.Set("CorrelationId", s);
            }
        };
    });

    // Status-code pages: re-execute the request through /error/{code} so
    // 401 / 403 / 404 / 5xx render the branded HTML page from ErrorController.
    // Skipped for /api/* paths so JSON callers still get ProblemDetails.
    app.UseWhen(
        ctx => !ctx.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
               && !ctx.Request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase)
               && !ctx.Request.Path.StartsWithSegments("/metrics", StringComparison.OrdinalIgnoreCase)
               && !ctx.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase),
        branch => branch.UseStatusCodePagesWithReExecute("/error/{0}"));

    app.UseMiddleware<GlobalExceptionMiddleware>();

    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    // Security headers BEFORE static files so 304 + cached static responses still carry them.
    app.UseMiddleware<SecurityHeadersMiddleware>();

    app.UseResponseCompression();
    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseResponseCaching();   // serves [ResponseCache(VaryByQueryKeys=...)] — see AddResponseCaching above
    app.UseOutputCache();

    // Per-IP rate limiter (login policy is attached to the specific actions).
    app.UseRateLimiter();

    var localization = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
    app.UseRequestLocalization(localization.Value);

    app.UseRouting();

    app.UseSession();
    app.UseAuthentication();
    app.UseAuthorization();

    // Tenant context runs AFTER authentication so we can read the user's
    // tenant claim with highest priority (falls back to Header / Subdomain /
    // Default chain when the request is anonymous or has no claim).
    app.UseMiddleware<TenantContextMiddleware>();

    // Optional per-tenant IP allow-list for /admin/* and /sysadmin/* routes.
    // No-op when `Admin.IpAllowList` is empty. Runs AFTER tenant context so
    // the per-tenant setting is resolvable.
    app.UseMiddleware<AdminIpAllowListMiddleware>();

    // Enforce one-time password change before any other navigation.
    app.UseMiddleware<MustChangePasswordMiddleware>();

    // Root "/" lands on HomeController which delegates to a role-aware
    // landing URL (or /Account/Login when anonymous). See HomeController.cs.
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    app.MapHub<KitchenDisplayHub>("/hubs/kitchen");

    // Prometheus scrape endpoint — `/metrics`. Pair with a scrape config:
    //   - job_name: 'canteen'   static_configs: [ targets: ['canteen.host:8080'] ]
    // The AddPrometheusExporter() call in TelemetryExtensions is the producer.
    app.MapPrometheusScrapingEndpoint();

    // Health probes (K8s livenessProbe / readinessProbe).
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false   // ignore all checks → just confirms the process is alive
    });
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = c => c.Tags.Contains("ready")
    });

    if (app.Environment.IsDevelopment() || app.Configuration.GetValue("OpenApi:Enabled", true))
    {
        app.UseCanteenOpenApi();
    }

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// Marker partial so WebApplicationFactory&lt;Program&gt; (integration tests)
/// can reference the implicit top-level-statement entry point class.
/// </summary>
public partial class Program { }
