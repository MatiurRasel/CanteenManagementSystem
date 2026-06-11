// =============================================================================
// TenantIsolationTests
// -----------------------------------------------------------------------------
// Verifies the EF global query filter + ClientId shadow-column stamping:
// an order created while tenant A (the seeded default tenant) is resolved
// must be invisible to a scope resolved as tenant B — both through the
// operator query path and through the raw DbContext.
//
// The per-scope RequestTenantContext is mutated exactly the way
// TenantContextMiddleware does it (ITenantContext as IMutableTenantContext).
// =============================================================================

using CanteenManagementSystem.Application.Operators;
using CanteenManagementSystem.Application.Orders.Commands;
using CanteenManagementSystem.Application.Orders.Dtos;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Application.Abstractions.Tenancy;
using Platform.Application.Dispatch;
using Platform.Domain.Tenancy;
using Xunit;

namespace CanteenManagementSystem.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class TenantIsolationTests
{
    // Seeded by ClientsSeed from Tenancy:DefaultClientId / DefaultClientCode.
    private const int TenantAId = 1;
    private const string TenantACode = "SMARTCANTEEN";

    private readonly CanteenWebApplicationFactory _factory;

    public TenantIsolationTests(CanteenWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task OrderPlacedUnderTenantA_IsNotVisibleToTenantB()
    {
        const string userId = "IT-EMP-TENA";

        // ── Arrange: a second tenant row ────────────────────────────────────
        int tenantBId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var existing = await db.Clients.SingleOrDefaultAsync(c => c.ClientCode == "IT-TENANT-B");
            if (existing is null)
            {
                existing = new Client
                {
                    ClientCode = "IT-TENANT-B",
                    ClientName = "Tenant B (integration tests)",
                    ShortName = "ITB",
                    IsActive = true
                };
                db.Clients.Add(existing);
                await db.SaveChangesAsync();
            }
            tenantBId = existing.ClientId;
        }
        tenantBId.Should().NotBe(TenantAId);

        // ── Arrange + Act: place an order while TENANT A is resolved ───────
        int orderId;
        using (var scope = _factory.Services.CreateScope())
        {
            ResolveTenant(scope, TenantAId, TenantACode);

            var (foodItemId, dailyMenuId) = await TestData.ArrangeMenuAndWalletAsync(
                scope, userId, "IT Tenant Tehari", price: 30m, walletFunds: 300m);

            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            var placed = await dispatcher.SendAsync(new PlaceOrderCommand(new PlaceOrderRequestDto
            {
                UserId = userId,
                UserIdentifier = userId,
                UserType = CanteenUserType.Employee,
                IdempotencyKey = Guid.NewGuid().ToString("N"),
                Items = { new OrderItemRequestDto { FoodItemId = foodItemId, DailyMenuId = dailyMenuId, Quantity = 1 } }
            }));

            placed.Success.Should().BeTrue(placed.Message);
            orderId = placed.OrderId;
        }

        // Sanity: the row really is stamped with tenant A's ClientId.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stampedClientId = await db.Orders
                .Where(o => o.OrderID == orderId)
                .Select(o => EF.Property<int?>(o, "ClientId"))
                .SingleAsync();
            stampedClientId.Should().Be(TenantAId);
        }

        // ── Assert: tenant A sees it through the operator query path ───────
        using (var scope = _factory.Services.CreateScope())
        {
            ResolveTenant(scope, TenantAId, TenantACode);
            var orders = await scope.ServiceProvider
                .GetRequiredService<IOperatorQueryService>()
                .GetOrdersAsync();
            orders.Should().Contain(o => o.OrderID == orderId,
                "the owning tenant must see its own order");
        }

        // ── Assert: tenant B does NOT see it — query service and DbContext ─
        using (var scope = _factory.Services.CreateScope())
        {
            ResolveTenant(scope, tenantBId, "IT-TENANT-B");

            var orders = await scope.ServiceProvider
                .GetRequiredService<IOperatorQueryService>()
                .GetOrdersAsync();
            orders.Should().NotContain(o => o.OrderID == orderId,
                "tenant B must not see tenant A's orders through the query path");

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Orders.AnyAsync(o => o.OrderID == orderId))
                .Should().BeFalse("the EF global query filter must hide cross-tenant rows");
        }
    }

    /// <summary>Mutate the scope's tenant context exactly like TenantContextMiddleware.</summary>
    private static void ResolveTenant(IServiceScope scope, int clientId, string clientCode)
    {
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenant.Should().BeAssignableTo<IMutableTenantContext>();
        ((IMutableTenantContext)tenant).Resolve(clientId, clientCode);
    }
}
