// =============================================================================
// StartupSeedingExtensions  (Platform.Presentation.Seeding)
// -----------------------------------------------------------------------------
// Binds the TWO SEPARATE option keys (Migration + Seed) and wires every
// platform-default ISeedContributor. Products plug in their own contributors
// with services.AddScoped<ISeedContributor, MyProductSeed>().
//
// Program.cs:
//   builder.Services.AddPlatformSeeding(builder.Configuration);
//   ...
//   await app.RunPlatformSeedAsync();
// =============================================================================

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Application.Abstractions.Seeding;
using Platform.Application.Configuration;
using Platform.Infrastructure.Seeding;
using Platform.Infrastructure.Seeding.Defaults;

namespace Platform.Presentation.Seeding;

public static class StartupSeedingExtensions
{
    public static IServiceCollection AddPlatformSeeding(this IServiceCollection services, IConfiguration configuration)
    {
        // Two separate top-level keys — migrations and seeding are independent.
        services.AddOptions<MigrationOptions>().BindConfiguration("Migration");
        services.AddOptions<SeedOptions>().BindConfiguration("Seed");

        services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();

        // ─── Platform default seed contributors (Order ascending) ─────────
        services.AddScoped<ISeedContributor, ClientsSeed>();       // Order  5 (tenant first)
        services.AddScoped<ISeedContributor, PermissionsSeed>();   // Order 10
        services.AddScoped<ISeedContributor, RolesSeed>();         // Order 20
        services.AddScoped<ISeedContributor, UsersSeed>();         // Order 30
        services.AddScoped<ISeedContributor, GatewaysSeed>();      // Order 40

        return services;
    }

    public static async Task RunPlatformSeedAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
        await seeder.SeedAsync();
    }
}
