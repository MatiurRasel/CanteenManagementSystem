// =============================================================================
// SandboxSeedContributor  (CanteenManagementSystem.Infrastructure.Seeding)
// -----------------------------------------------------------------------------
// When `Sandbox.Enabled` is true, seeds demo fixtures so the system shows
// realistic data on first run without manual data entry.
//
// SEEDS (only when missing — idempotent)
//   * 3 sandbox combos (Lunch combo, Snack pack, Breakfast deal)
//   * 2 suppliers (Asia Foods, City Bakery)
//   * 1 push subscription (synthetic; not a real browser endpoint)
//   * Marks billing as Active on the sandbox provider
//
// Sandbox.Enabled defaults to FALSE — flip via appsettings or a TenantSetting.
// This contributor runs after the platform's default seeders (Order=300).
// =============================================================================

using CanteenManagementSystem.Domain.Inventory;
using CanteenManagementSystem.Domain.Menu;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Abstractions.Seeding;
using Platform.Application.Persistence;

namespace CanteenManagementSystem.Infrastructure.Seeding;

internal sealed class SandboxSeedContributor : ISeedContributor
{
    public int Order => 300;     // after platform (10..40) + CanteenSeed (100)

    private readonly IAppDbContext _db;
    private readonly ITenantSettings _settings;
    private readonly ILogger<SandboxSeedContributor> _logger;

    public SandboxSeedContributor(IAppDbContext db, ITenantSettings settings, ILogger<SandboxSeedContributor> logger)
    {
        _db = db; _settings = settings; _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var enabled = await _settings.GetBoolAsync("Sandbox.Enabled", false, cancellationToken);
        if (!enabled) return;

        _logger.LogInformation("Sandbox mode is on — seeding demo fixtures.");

        await SeedCombosAsync(cancellationToken);
        await SeedSuppliersAsync(cancellationToken);
        await SeedBillingAsync(cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedCombosAsync(CancellationToken ct)
    {
        var hasCombos = await _db.Set<ComboButton>().AnyAsync(ct);
        if (hasCombos) return;
        // We don't FK to FoodItem ids — combo just references CSV. Use the first
        // three active FoodItem ids if available, otherwise placeholders.
        var foodIds = await _db.Set<FoodItem>()
            .Where(f => f.IsActive)
            .OrderBy(f => f.FoodItemID)
            .Take(6)
            .Select(f => f.FoodItemID)
            .ToListAsync(ct);
        if (foodIds.Count < 2) return;     // not worth seeding combos without items

        _db.Set<ComboButton>().AddRange(
            new ComboButton { Code = "LUNCH",    DisplayName = "Lunch combo",     Color = "#6366f1", SortOrder = 1, FoodItemIdsCsv = string.Join(',', foodIds.Take(3)), IsActive = true },
            new ComboButton { Code = "SNACK",    DisplayName = "Snack pack",      Color = "#f59e0b", SortOrder = 2, FoodItemIdsCsv = string.Join(',', foodIds.Take(2)), IsActive = true },
            new ComboButton { Code = "BFAST",    DisplayName = "Breakfast deal",  Color = "#10b981", SortOrder = 3, FoodItemIdsCsv = string.Join(',', foodIds.Take(2)), IsActive = true }
        );
    }

    private async Task SeedSuppliersAsync(CancellationToken ct)
    {
        var hasSuppliers = await _db.Set<Supplier>().AnyAsync(ct);
        if (hasSuppliers) return;
        _db.Set<Supplier>().AddRange(
            new Supplier { Name = "Asia Foods Ltd",  ContactNo = "+8801711000001", Email = "sales@asiafoods.bd",  IsActive = true },
            new Supplier { Name = "City Bakery",     ContactNo = "+8801711000002", Email = "orders@citybakery.bd", IsActive = true }
        );
    }

    private async Task SeedBillingAsync(CancellationToken ct)
    {
        var existingPlan = await _settings.GetAsync("Billing.Sandbox.PlanCode", defaultValue: null, ct);
        if (!string.IsNullOrEmpty(existingPlan)) return;
        await _settings.SetAsync("Billing.Sandbox.PlanCode",     "pro",      cancellationToken: ct);
        await _settings.SetAsync("Billing.Sandbox.State",         "Active",   cancellationToken: ct);
        await _settings.SetAsync("Billing.Sandbox.PeriodEndUtc",  DateTime.UtcNow.AddDays(30).ToString("O"), cancellationToken: ct);
        await _settings.SetAsync("Billing.Sandbox.LastInvoiceId", $"sbx_{DateTime.UtcNow:yyyyMMddHHmmss}",   cancellationToken: ct);
    }
}
