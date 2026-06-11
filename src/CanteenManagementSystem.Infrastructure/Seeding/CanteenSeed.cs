// =============================================================================
// CanteenSeed  (CanteenManagementSystem.Infrastructure.Seeding)
// -----------------------------------------------------------------------------
// Product-level ISeedContributor for the Canteen vertical. Order = 100,
// so it runs AFTER every platform-default contributor (10..40).
//
// SCOPE
//   * Default FoodItems — one per CanteenMealType. Tenants edit / add via
//     /Menu/Manage; the seed only guarantees a non-empty starter menu.
//   * NO tenant-scoped data (Orders, Wallets, Cards) — those grow from real
//     user activity, never from seed.
//
// IDEMPOTENCY
//   Business key is FoodItem.ItemName. Existing rows are left untouched.
//
// MULTI-TENANCY NOTE
//   This seed runs ONCE per database, not per tenant. The ClientId shadow
//   property is stamped by ITenantContext at INSERT time. Since the
//   DatabaseSeeder runs at startup with NullTenantContext, these rows go
//   into the DEFAULT tenant. Per-tenant feature seeds belong in an admin
//   "Provision new tenant" flow, not here.
// =============================================================================

using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Menu;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Application.Abstractions.Seeding;
using Platform.Application.Persistence;

namespace CanteenManagementSystem.Infrastructure.Seeding;

public sealed class CanteenSeed : ISeedContributor
{
    public int Order => 100;

    public static readonly IReadOnlyList<FoodItemDefinition> Defaults = new[]
    {
        new FoodItemDefinition("Paratha + Egg",     "Two parathas with a fried egg.",    35m,  CanteenMealType.Breakfast),
        new FoodItemDefinition("Khichuri",          "Hot khichuri with pickle.",         50m,  CanteenMealType.Lunch),
        new FoodItemDefinition("Beef Tehari",       "Spiced rice with tender beef.",     120m, CanteenMealType.Lunch),
        new FoodItemDefinition("Vegetable Roll",    "Crisp roll with mixed veg.",        40m,  CanteenMealType.Snacks),
        new FoodItemDefinition("Samosa",            "Two samosas with chutney.",         20m,  CanteenMealType.Snacks),
        new FoodItemDefinition("Lemon Tea",         "Black tea with lemon.",             15m,  CanteenMealType.Drinks),
        new FoodItemDefinition("Mineral Water 500ml","Sealed bottle.",                   20m,  CanteenMealType.Drinks),
        new FoodItemDefinition("Chicken Curry + Rice","Steamed rice with chicken curry.",100m, CanteenMealType.Dinner),
    };

    private readonly IAppDbContext _db;
    private readonly ILogger<CanteenSeed> _logger;

    public CanteenSeed(IAppDbContext db, ILogger<CanteenSeed> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var foods = _db.Set<FoodItem>();
        var inserted = 0;
        foreach (var def in Defaults)
        {
            if (await foods.AnyAsync(f => f.ItemName == def.Name, cancellationToken)) continue;
            foods.Add(new FoodItem
            {
                ItemName = def.Name,
                Description = def.Description,
                Price = def.Price,
                Category = def.Category,
                IsAvailable = true,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            });
            inserted++;
        }
        if (inserted > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Seeded {Count} default food item(s) for the canteen product.", inserted);
        }
    }

    public sealed record FoodItemDefinition(
        string Name,
        string Description,
        decimal Price,
        CanteenMealType Category);
}
