// =============================================================================
// InventoryService  (CanteenManagementSystem.Infrastructure.Inventory)
// -----------------------------------------------------------------------------
// Reserve / Consume / Release transitions on DailyMenu rows.
//
// LAYERING (ADR 0004, Batch 2)
//   Depends on IRepository<DailyMenu>, NOT IAppDbContext. The repo's Update()
//   marks the entity dirty; SaveChanges is the responsibility of the
//   surrounding command handler (TransactionBehavior in the dispatcher pipeline
//   commits inventory + wallet + audit atomically).
//
// NOTE on Update()
//   EF tracks the loaded entity automatically via Query()/FirstOrDefaultAsync,
//   so mutating menu.ReservedQuantity is sufficient — we don't need to call
//   _repo.Update(menu) explicitly. The calls are still added for clarity
//   and to keep the contract obvious to readers.
// =============================================================================

using Platform.Application.Persistence;
using Platform.Application.Abstractions.Inventory;
using Platform.Application.Abstractions.RealTime;
using Platform.Application.Results;
using CanteenManagementSystem.Domain.Menu;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanteenManagementSystem.Infrastructure.Inventory;

internal sealed class InventoryService : IInventoryService
{
    private readonly IRepository<DailyMenu> _menus;
    private readonly IOrderBroadcaster _broadcaster;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(IRepository<DailyMenu> menus, IOrderBroadcaster broadcaster, ILogger<InventoryService> logger)
    {
        _menus = menus; _broadcaster = broadcaster; _logger = logger;
    }

    public async Task<Result> ReserveAsync(int dailyMenuId, int quantity, CancellationToken cancellationToken = default)
    {
        if (quantity <= 0) return Result.Failure(Error.Validation("Quantity must be positive."));

        var menu = await _menus.FirstOrDefaultAsync(dm => dm.DailyMenuID == dailyMenuId, cancellationToken);
        if (menu is null) return Result.Failure(Error.NotFound("Daily menu item not found."));
        if (!menu.IsAvailable) return Result.Failure(Error.Conflict("Item is unavailable."));

        var available = menu.AvailableQuantity - menu.ReservedQuantity;
        if (available < quantity)
        {
            return Result.Failure(Error.Conflict($"Only {available} item(s) remain."));
        }

        menu.ReservedQuantity += quantity;
        _menus.Update(menu);
        return Result.Success();
    }

    public async Task<Result> ConsumeAsync(int dailyMenuId, int quantity, CancellationToken cancellationToken = default)
    {
        var menu = await _menus.Query()
            .Include(dm => dm.FoodItem)
            .FirstOrDefaultAsync(dm => dm.DailyMenuID == dailyMenuId, cancellationToken);
        if (menu is null) return Result.Failure(Error.NotFound("Daily menu item not found."));

        var wasAvailable = menu.IsAvailable && menu.AvailableQuantity > 0;
        var reserved = Math.Min(menu.ReservedQuantity, quantity);
        menu.ReservedQuantity -= reserved;
        menu.AvailableQuantity = Math.Max(0, menu.AvailableQuantity - quantity);
        if (menu.AvailableQuantity <= 0) menu.IsAvailable = false;
        _menus.Update(menu);

        // Push a stock-out event the moment we cross from "available" to "0".
        // Best-effort — broadcast failures must NOT roll back the consume.
        if (wasAvailable && menu.AvailableQuantity <= 0)
        {
            try
            {
                await _broadcaster.StockOutAsync(new
                {
                    dailyMenuId = menu.DailyMenuID,
                    foodItemId  = menu.FoodItemID,
                    itemName    = menu.FoodItem?.ItemName ?? string.Empty
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Stock-out broadcast failed for DailyMenu {Id}", menu.DailyMenuID);
            }
        }

        return Result.Success();
    }

    public async Task<Result> ReleaseAsync(int dailyMenuId, int quantity, CancellationToken cancellationToken = default)
    {
        var menu = await _menus.FirstOrDefaultAsync(dm => dm.DailyMenuID == dailyMenuId, cancellationToken);
        if (menu is null) return Result.Failure(Error.NotFound("Daily menu item not found."));

        var release = Math.Min(menu.ReservedQuantity, quantity);
        menu.ReservedQuantity -= release;
        _menus.Update(menu);
        return Result.Success();
    }
}
