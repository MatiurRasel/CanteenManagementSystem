// =============================================================================
// OrderLifecycleTests — the launch gate.
// -----------------------------------------------------------------------------
// Exercises the REAL order money-flow end to end through the production
// dispatcher pipeline (validation + transaction behaviors), the real wallet
// service and a real SQL Server database:
//
//   Place   → wallet funds BLOCKED   + OrderBlock ledger row
//   Deliver → block settled to USED  + DeliveryDeduction ledger row
//   Cancel  → block RELEASED         + OrderRelease ledger row
//
// Status path mirrors the operator flow: Placed → Confirmed → Preparing →
// Ready (operator dashboard) → MarkOrderDeliveredCommand (counter delivery).
// =============================================================================

using CanteenManagementSystem.Application.Operators;
using CanteenManagementSystem.Application.Orders.Commands;
using CanteenManagementSystem.Application.Orders.Dtos;
using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Wallets;
using CanteenManagementSystem.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Application.Dispatch;
using Xunit;

namespace CanteenManagementSystem.IntegrationTests;

[Collection(IntegrationCollection.Name)]
public sealed class OrderLifecycleTests
{
    private readonly CanteenWebApplicationFactory _factory;

    public OrderLifecycleTests(CanteenWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PlaceThenDeliver_BlocksThenDeductsWalletFunds_WithLedgerTrail()
    {
        const string userId = "IT-EMP-LIFE";
        var idempotencyKey = Guid.NewGuid().ToString("N");
        int dailyMenuId, foodItemId;

        // ── Arrange: menu item @40 BDT, wallet funded with 500 BDT ─────────
        using (var scope = _factory.Services.CreateScope())
        {
            (foodItemId, dailyMenuId) = await TestData.ArrangeMenuAndWalletAsync(
                scope, userId, "IT Lifecycle Khichuri", price: 40m, walletFunds: 500m);
        }

        // ── Act 1: place the order (3 × 40 = 120) ──────────────────────────
        OrderResponseDto placed;
        using (var scope = _factory.Services.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            placed = await dispatcher.SendAsync(new PlaceOrderCommand(new PlaceOrderRequestDto
            {
                UserId = userId,
                UserIdentifier = userId,
                UserType = CanteenUserType.Employee,
                IdempotencyKey = idempotencyKey,
                Items = { new OrderItemRequestDto { FoodItemId = foodItemId, DailyMenuId = dailyMenuId, Quantity = 3 } }
            }));
        }

        placed.Success.Should().BeTrue(placed.Message);
        placed.OrderId.Should().BePositive();
        placed.TotalAmount.Should().Be(120m);

        // ── Assert 1: order Placed, funds blocked, OrderBlock ledger row ───
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var order = await db.Orders.AsNoTracking().SingleAsync(o => o.OrderID == placed.OrderId);
            order.Status.Should().Be(CanteenOrderStatus.Placed);
            order.TotalAmount.Should().Be(120m);

            var balance = await db.UserBalances.AsNoTracking().SingleAsync(b => b.UserId == userId);
            balance.TotalBalance.Should().Be(500m);
            balance.BlockedAmount.Should().Be(120m);
            balance.UsedBalance.Should().Be(0m);
            balance.AvailableBalance.Should().Be(380m);

            var blockRow = await db.WalletLedger.AsNoTracking()
                .SingleAsync(l => l.OrderID == placed.OrderId && l.EntryType == WalletLedgerEntryType.OrderBlock);
            blockRow.Amount.Should().Be(120m);
            blockRow.UserId.Should().Be(userId);
            blockRow.IdempotencyKey.Should().Be(idempotencyKey);

            var menu = await db.DailyMenus.AsNoTracking().SingleAsync(m => m.DailyMenuID == dailyMenuId);
            menu.ReservedQuantity.Should().Be(3);
        }

        // ── Act 1b: replay the same idempotency key → cached result, no double block
        using (var scope = _factory.Services.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            var replay = await dispatcher.SendAsync(new PlaceOrderCommand(new PlaceOrderRequestDto
            {
                UserId = userId,
                UserIdentifier = userId,
                UserType = CanteenUserType.Employee,
                IdempotencyKey = idempotencyKey,
                Items = { new OrderItemRequestDto { FoodItemId = foodItemId, DailyMenuId = dailyMenuId, Quantity = 3 } }
            }));

            replay.Success.Should().BeTrue();
            replay.OrderId.Should().Be(placed.OrderId);

            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Orders.CountAsync(o => o.IdempotencyKey == idempotencyKey)).Should().Be(1);
            (await db.UserBalances.AsNoTracking().SingleAsync(b => b.UserId == userId))
                .BlockedAmount.Should().Be(120m, "a replayed order must not block funds twice");
        }

        // ── Act 2: operator advances the kitchen flow, then delivers ───────
        using (var scope = _factory.Services.CreateScope())
        {
            var operators = scope.ServiceProvider.GetRequiredService<IOperatorQueryService>();
            foreach (var status in new[] { CanteenOrderStatus.Confirmed, CanteenOrderStatus.Preparing, CanteenOrderStatus.Ready })
            {
                var step = await operators.UpdateOrderStatusAsync(placed.OrderId, (int)status);
                step.Success.Should().BeTrue(step.Message);
            }
        }

        OrderStatusUpdateResultDto delivered;
        using (var scope = _factory.Services.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            delivered = await dispatcher.SendAsync(new MarkOrderDeliveredCommand(placed.OrderId));
        }

        delivered.Success.Should().BeTrue(delivered.Message);

        // ── Assert 2: Delivered, funds moved Blocked → Used, Deduction row ─
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var order = await db.Orders.AsNoTracking().SingleAsync(o => o.OrderID == placed.OrderId);
            order.Status.Should().Be(CanteenOrderStatus.Delivered);
            order.DeliveredDate.Should().NotBeNull();

            var balance = await db.UserBalances.AsNoTracking().SingleAsync(b => b.UserId == userId);
            balance.TotalBalance.Should().Be(500m);
            balance.BlockedAmount.Should().Be(0m, "delivery settles the block");
            balance.UsedBalance.Should().Be(120m, "delivery deducts the order amount");
            balance.AvailableBalance.Should().Be(380m);

            var deductRow = await db.WalletLedger.AsNoTracking()
                .SingleAsync(l => l.OrderID == placed.OrderId && l.EntryType == WalletLedgerEntryType.DeliveryDeduction);
            deductRow.Amount.Should().Be(120m);

            var menu = await db.DailyMenus.AsNoTracking().SingleAsync(m => m.DailyMenuID == dailyMenuId);
            menu.ReservedQuantity.Should().Be(0);
            menu.AvailableQuantity.Should().Be(97, "3 units were consumed on delivery");
        }
    }

    [Fact]
    public async Task PlaceThenCancel_ReleasesBlockedFunds_WithReleaseLedgerRow()
    {
        const string userId = "IT-EMP-CANCL";
        var idempotencyKey = Guid.NewGuid().ToString("N");
        int dailyMenuId, foodItemId;

        using (var scope = _factory.Services.CreateScope())
        {
            (foodItemId, dailyMenuId) = await TestData.ArrangeMenuAndWalletAsync(
                scope, userId, "IT Cancel Samosa", price: 25m, walletFunds: 200m);
        }

        OrderResponseDto placed;
        using (var scope = _factory.Services.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            placed = await dispatcher.SendAsync(new PlaceOrderCommand(new PlaceOrderRequestDto
            {
                UserId = userId,
                UserIdentifier = userId,
                UserType = CanteenUserType.Employee,
                IdempotencyKey = idempotencyKey,
                Items = { new OrderItemRequestDto { FoodItemId = foodItemId, DailyMenuId = dailyMenuId, Quantity = 2 } }
            }));
        }

        placed.Success.Should().BeTrue(placed.Message);
        placed.TotalAmount.Should().Be(50m);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.UserBalances.AsNoTracking().SingleAsync(b => b.UserId == userId))
                .BlockedAmount.Should().Be(50m);
        }

        // ── Act: operator voids the order before delivery ──────────────────
        OrderStatusUpdateResultDto voided;
        using (var scope = _factory.Services.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
            voided = await dispatcher.SendAsync(new VoidOrderCommand(placed.OrderId, "integration-test cancel"));
        }

        voided.Success.Should().BeTrue(voided.Message);

        // ── Assert: Cancelled, block released, wallet whole, ledger correct ─
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var order = await db.Orders.AsNoTracking().SingleAsync(o => o.OrderID == placed.OrderId);
            order.Status.Should().Be(CanteenOrderStatus.Cancelled);

            var balance = await db.UserBalances.AsNoTracking().SingleAsync(b => b.UserId == userId);
            balance.TotalBalance.Should().Be(200m);
            balance.BlockedAmount.Should().Be(0m, "cancelling releases the block");
            balance.UsedBalance.Should().Be(0m, "no funds are consumed on cancel");
            balance.AvailableBalance.Should().Be(200m);

            var releaseRow = await db.WalletLedger.AsNoTracking()
                .SingleAsync(l => l.OrderID == placed.OrderId && l.EntryType == WalletLedgerEntryType.OrderRelease);
            releaseRow.Amount.Should().Be(50m);

            (await db.WalletLedger.AsNoTracking()
                .AnyAsync(l => l.OrderID == placed.OrderId && l.EntryType == WalletLedgerEntryType.DeliveryDeduction))
                .Should().BeFalse("a cancelled order must never deduct funds");

            var menu = await db.DailyMenus.AsNoTracking().SingleAsync(m => m.DailyMenuID == dailyMenuId);
            menu.ReservedQuantity.Should().Be(0, "cancel restores the stock reservation");
            menu.AvailableQuantity.Should().Be(100);
        }
    }
}
