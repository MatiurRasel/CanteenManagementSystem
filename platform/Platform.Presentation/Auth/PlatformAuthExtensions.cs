// =============================================================================
// PlatformAuthExtensions  (Platform.Presentation.Auth)
// -----------------------------------------------------------------------------
// One-call setup for all THREE auth schemes + the policy graph.
//
//   builder.Services.AddPlatformAuth(builder.Configuration);
//   app.UseAuthentication();
//   app.UseAuthorization();
//
// Default scheme = Cookie. Why: browser navigations land on the MVC views and
// the [Authorize] redirect to /Account/Login depends on a cookie-style
// challenge handler. API routes still authenticate via Bearer/ApiKey because
// each [Authorize] policy declares it accepts ALL THREE schemes — the runtime
// picks whichever the inbound request actually carries.
// =============================================================================

using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Platform.Application.Abstractions.Auth;
using Platform.Application.Configuration;
using Platform.Infrastructure.Auth;

namespace Platform.Presentation.Auth;

public static class PlatformAuthExtensions
{
    public static IServiceCollection AddPlatformAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthOptions>().BindConfiguration("Auth");

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddSingleton<TotpService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IApiKeyAuthorizer, ApiKeyAuthorizer>();
        services.AddScoped<Platform.Application.Abstractions.Auth.IMfaRecoveryCodeService,
                           MfaRecoveryCodeService>();
        services.AddScoped<Platform.Application.Abstractions.Auth.IMfaService,
                           MfaService>();
        services.AddScoped<Platform.Application.Abstractions.Auth.IKioskDeviceService,
                           KioskDeviceService>();
        services.AddScoped<Platform.Application.Abstractions.Auth.IAuthTokenService,
                           AuthTokenService>();
        services.AddScoped<Platform.Application.Abstractions.Tenancy.ITenantAdminService,
                           Platform.Infrastructure.Tenancy.TenantAdminService>();
        services.AddScoped<Platform.Application.Abstractions.Identity.IUserAdminService,
                           Platform.Infrastructure.Identity.UserAdminService>();

        var auth = configuration.GetSection("Auth").Get<AuthOptions>() ?? new AuthOptions();
        var signingBytes = string.IsNullOrEmpty(auth.SigningKey)
            ? new byte[32]
            : Encoding.UTF8.GetBytes(auth.SigningKey);

        services
            .AddAuthentication(PlatformAuthSchemes.Cookie)     // browsers land here

            // ─── 1. Cookie (browser MVC views) ───────────────────────────
            .AddCookie(PlatformAuthSchemes.Cookie, options =>
            {
                options.Cookie.Name        = "ccs.session";
                options.Cookie.HttpOnly    = true;
                options.Cookie.SameSite    = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
                options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan     = TimeSpan.FromHours(8);
                options.SlidingExpiration  = true;
                options.LoginPath          = "/Account/Login";
                options.LogoutPath         = "/Account/Logout";
                options.AccessDeniedPath   = "/Account/AccessDenied";
            })

            // ─── 2. JWT Bearer (SPA / mobile / SDK) ──────────────────────
            .AddJwtBearer(PlatformAuthSchemes.Bearer, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = new SymmetricSecurityKey(signingBytes),
                    ValidateIssuer           = true,
                    ValidIssuer              = auth.Issuer,
                    ValidateAudience         = true,
                    ValidAudience            = auth.Audience,
                    ValidateLifetime         = true,
                    ClockSkew                = TimeSpan.FromSeconds(30),
                    RoleClaimType            = System.Security.Claims.ClaimTypes.Role
                };
            })

            // ─── 3. HMAC API Key (service-to-service) ────────────────────
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                PlatformAuthSchemes.ApiKey, _ => { });

        // ─── 4. Google SSO (optional, opt-in via config) ─────────────────
        // Wire AddGoogle ONLY when Auth:Google:ClientId is non-empty so the
        // app boots cleanly without the secret. Get a client ID from
        // https://console.cloud.google.com → APIs & Services → Credentials.
        // Authorized redirect URI: https://your-domain/signin-google
        var googleClientId = configuration["Auth:Google:ClientId"];
        var googleClientSecret = configuration["Auth:Google:ClientSecret"];
        if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
        {
            services.AddAuthentication()
                .AddGoogle("Google", options =>
                {
                    options.ClientId     = googleClientId;
                    options.ClientSecret = googleClientSecret;
                    options.SignInScheme = PlatformAuthSchemes.External;
                    options.SaveTokens   = false;
                    options.CallbackPath = "/signin-google";
                });
            services.AddAuthentication()
                .AddCookie(PlatformAuthSchemes.External, options =>
                {
                    options.Cookie.Name = "ccs.external";
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
                });
        }

        // ─── Policies ────────────────────────────────────────────────────
        //   Every role-style policy accepts all three schemes so the same
        //   [Authorize(Policy = ...)] decorator works for cookie + JWT + API key.
        var allSchemes = new[] { PlatformAuthSchemes.Cookie, PlatformAuthSchemes.Bearer, PlatformAuthSchemes.ApiKey };

        services.AddAuthorization(opt =>
        {
            opt.AddPolicy("SystemAdmin",  p => p.AddAuthenticationSchemes(allSchemes).RequireRole("SystemAdmin"));
            opt.AddPolicy("TenantAdmin",  p => p.AddAuthenticationSchemes(allSchemes).RequireRole("SystemAdmin", "TenantAdmin"));
            opt.AddPolicy("Operator",     p => p.AddAuthenticationSchemes(allSchemes).RequireRole("SystemAdmin", "TenantAdmin", "Operator", "Cashier"));
            opt.AddPolicy("Auditor",      p => p.AddAuthenticationSchemes(allSchemes).RequireRole("SystemAdmin", "TenantAdmin", "Auditor"));

            opt.AddPolicy("AuthenticatedAny", p =>
                p.AddAuthenticationSchemes(allSchemes).RequireAuthenticatedUser());

            // Kiosk surface accepts either a real operator OR a paired kiosk device.
            opt.AddPolicy("OperatorOrKiosk", p =>
                p.AddAuthenticationSchemes(allSchemes).RequireRole("SystemAdmin", "TenantAdmin", "Operator", "Cashier", "Kiosk"));
        });

        return services;
    }
}
