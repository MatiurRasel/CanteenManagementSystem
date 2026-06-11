// =============================================================================
// CanteenWebApplicationFactory  (CanteenManagementSystem.IntegrationTests)
// -----------------------------------------------------------------------------
// Boots the REAL application (Program.cs top-level statements) against a real
// SQL Server database:
//
//   * Connection string comes from the CANTEEN_TEST_DB env var (CI: a
//     mcr.microsoft.com/mssql/server service container) and falls back to
//     LocalDB for local dev runs.
//   * The database is dropped once per test collection (constructor) so every
//     run starts from a clean slate; the app's own startup pipeline then runs
//     Database.MigrateAsync() + the ISeedContributor chain (ClientsSeed →
//     PermissionsSeed → RolesSeed → UsersSeed → GatewaysSeed → CanteenSeed).
//   * Real SQL Server is mandatory — 9 entities map rowversion concurrency
//     tokens (.IsRowVersion()) that Sqlite/InMemory cannot emulate.
//   * Every app-level IHostedService is removed (webhook dispatcher, retention
//     sweepers, LocalNbrGatewaySimulator which binds 127.0.0.1:5099, ...) so
//     tests are deterministic and port-collision free. Framework hosted
//     services (GenericWebHostService etc.) are kept — the TestServer needs
//     them to start.
//
// Shared once per collection via ICollectionFixture<CanteenWebApplicationFactory>
// (see IntegrationCollection) — migration + seed run exactly once per test run.
// =============================================================================

using CanteenManagementSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CanteenManagementSystem.IntegrationTests;

public sealed class CanteenWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>CI overrides via CANTEEN_TEST_DB; local runs use LocalDB.</summary>
    public static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("CANTEEN_TEST_DB")
        ?? @"Server=(localdb)\MSSQLLocalDB;Database=SmartCanteen_IT;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

    public CanteenWebApplicationFactory()
    {
        // Clean slate BEFORE the host boots — startup then migrates + seeds.
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        using var ctx = new ApplicationDbContext(options);
        ctx.Database.EnsureDeleted();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        builder.UseSetting("ConnectionStrings:Redis", "");
        builder.UseSetting("Auth:SigningKey",
            "integration-test-signing-key-3c1f9a7e54b24d0c8e6f2a1b9d8c7e6f-0123456789abcdef");
        builder.UseSetting("Migration:ApplyOnStartup", "true");
        builder.UseSetting("Migration:RetryCount", "0");
        builder.UseSetting("Seed:RunOnStartup", "true");

        // Keep test output readable — warnings and errors only.
        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");
        builder.UseSetting("Logging:LogLevel:Default", "Warning");

        builder.ConfigureServices(services =>
        {
            // Remove the app's background workers (timers, port-binding
            // simulators, dispatch loops). Keep framework hosted services —
            // removing GenericWebHostService would stop the TestServer booting.
            var appHostedServices = services
                .Where(d => d.ServiceType == typeof(IHostedService)
                            && d.ImplementationType is { FullName: not null }
                            && (d.ImplementationType.FullName.StartsWith("Platform.", StringComparison.Ordinal)
                                || d.ImplementationType.FullName.StartsWith("CanteenManagementSystem.", StringComparison.Ordinal)))
                .ToList();

            foreach (var descriptor in appHostedServices)
            {
                services.Remove(descriptor);
            }
        });
    }
}
