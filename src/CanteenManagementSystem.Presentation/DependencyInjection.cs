// =============================================================================
// Presentation.DependencyInjection
// -----------------------------------------------------------------------------
// Registers everything that is *web-specific*: MVC, JSON, localisation,
// session, HTTP context, tenant resolution, OpenAPI, etc.
//
// FLOW (boot sequence)
// --------------------
// Program.cs calls:
//   1. AddCanteenApplication(...)      -> handlers, behaviors, options
//   2. AddCanteenInfrastructure(...)   -> DbContext, cache, wallet, services
//   3. AddCanteenPresentation()        -> THIS FILE (MVC + tenant chrome)
//   4. AddCanteenOpenApi()             -> Swashbuckle + ApiVersioning
//   5. app.UseMiddleware<TenantContextMiddleware>() ahead of routing
//
// WHY tenant context lives here, not in Infrastructure
// ----------------------------------------------------
// The tenant identity is resolved from `HttpContext` (header / subdomain /
// query). Infrastructure must not depend on ASP.NET Core, so the *resolution*
// strategies are registered here and the *consumption* (DbContext query
// filter) reads via the Domain abstraction `ITenantContext`.
// =============================================================================

using System.Globalization;
using Platform.Application.Abstractions.Identity;
using Platform.Domain.Tenancy;
using Platform.Presentation.Auth;
using Platform.Presentation.Middleware.Tenancy;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanteenManagementSystem.Presentation;

public static class DependencyInjection
{
    public static IServiceCollection AddCanteenPresentation(this IServiceCollection services)
    {
        // -------------------- 1. MVC + JSON --------------------------------
        services.AddControllersWithViews()
            .AddViewLocalization()
            .AddDataAnnotationsLocalization()
            .AddJsonOptions(options =>
            {
                // Unicode escape is OFF so Bangla characters render natively
                // in API responses; property naming preserved as-declared.
                options.JsonSerializerOptions.Encoder =
                    System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
                options.JsonSerializerOptions.PropertyNamingPolicy = null;
            });

        // -------------------- 2. Localisation ------------------------------
        // Resource files live under /Resources/SharedResource.*.resx
        services.AddLocalization(options => options.ResourcesPath = "Resources");

        // -------------------- 3. Session + Distributed Cache ---------------
        // NOTE: the *real* distributed cache (Redis or fallback in-memory) is
        // registered in Infrastructure.AddCanteenInfrastructure. The line
        // below is a no-op when Redis is already registered, but keeps the
        // session feature working in unit-test scenarios where Infrastructure
        // is skipped.
        services.AddDistributedMemoryCache();
        services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromMinutes(30);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
        });

        // -------------------- 4. Request localisation ----------------------
        services.Configure<RequestLocalizationOptions>(options =>
        {
            var supportedCultures = new[]
            {
                new CultureInfo("en-US"),
                new CultureInfo("bn-BD")
            };

            options.DefaultRequestCulture = new RequestCulture("bn-BD");
            options.SupportedCultures = supportedCultures;
            options.SupportedUICultures = supportedCultures;

            options.RequestCultureProviders.Insert(0, new QueryStringRequestCultureProvider());
            options.RequestCultureProviders.Insert(1, new CookieRequestCultureProvider());
        });

        services.AddHttpContextAccessor();
        services.AddResponseCompression();

        // -------------------- 5. Current user -----------------------------
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        // -------------------- 6. Tenant chrome ----------------------------
        // The Infrastructure default `NullTenantContext` is swapped out here
        // for the mutable, per-request `RequestTenantContext`. The middleware
        // mutates it before any controller / handler runs.
        services.AddScoped<RequestTenantContext>();
        services.RemoveAll<ITenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<RequestTenantContext>());

        // Resolver chain: Header > Subdomain > Default. First non-null wins.
        services.AddScoped<HeaderTenantResolver>();
        services.AddScoped<SubdomainTenantResolver>();
        services.AddScoped<DefaultTenantResolver>();
        services.AddScoped<ITenantResolver>(sp => new CompositeTenantResolver(new ITenantResolver[]
        {
            sp.GetRequiredService<HeaderTenantResolver>(),
            sp.GetRequiredService<SubdomainTenantResolver>(),
            sp.GetRequiredService<DefaultTenantResolver>()
        }));

        return services;
    }
}
