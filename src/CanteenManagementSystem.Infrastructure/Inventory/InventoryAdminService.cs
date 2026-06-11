// =============================================================================
// InventoryAdminService  (CanteenManagementSystem.Infrastructure.Inventory)
// -----------------------------------------------------------------------------
// One implementation, four sub-domains: Suppliers / POs / Stock takes / Waste.
// ADR 0004 — IUnitOfWork + IRepository / IReadOnlyRepository only.
// =============================================================================

using CanteenManagementSystem.Application.Inventory;
using CanteenManagementSystem.Domain.Inventory;
using CanteenManagementSystem.Domain.Menu;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Abstractions.Audit;
using Platform.Application.Persistence;

namespace CanteenManagementSystem.Infrastructure.Inventory;

internal sealed class InventoryAdminService : IInventoryAdminService
{
    private readonly IUnitOfWork _uow;
    private readonly IReadOnlyRepository<FoodItem> _foods;
    private readonly IAuditTrail _audit;

    public InventoryAdminService(IUnitOfWork uow, IReadOnlyRepository<FoodItem> foods, IAuditTrail audit)
    {
        _uow = uow; _foods = foods; _audit = audit;
    }

    private IRepository<Supplier>          Suppliers     => _uow.Repository<Supplier>();
    private IRepository<PurchaseOrder>     Pos           => _uow.Repository<PurchaseOrder>();
    private IRepository<PurchaseOrderLine> PoLines       => _uow.Repository<PurchaseOrderLine>();
    private IRepository<StockTake>         StockTakes    => _uow.Repository<StockTake>();
    private IRepository<StockTakeLine>     StockLines    => _uow.Repository<StockTakeLine>();
    private IRepository<WasteLog>          Waste         => _uow.Repository<WasteLog>();
    private IRepository<DailyMenu>         DailyMenus    => _uow.Repository<DailyMenu>();

    // ─── Suppliers ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<Supplier>> ListSuppliersAsync(bool includeInactive, CancellationToken ct = default)
    {
        var q = Suppliers.NoTrackingQuery();
        if (!includeInactive) q = q.Where(s => s.IsActive);
        return await q.OrderBy(s => s.Name).ToListAsync(ct);
    }

    public Task<Supplier?> GetSupplierAsync(int id, CancellationToken ct = default)
        => Suppliers.FirstOrDefaultAsync(s => s.SupplierId == id, ct);

    public async Task<Supplier> CreateSupplierAsync(SupplierInput input, CancellationToken ct = default)
    {
        var s = new Supplier
        {
            Name = input.Name.Trim(),
            ContactName = input.ContactName, ContactNo = input.ContactNo,
            Email = input.Email, Address = input.Address, Notes = input.Notes,
            IsActive = input.IsActive, CreatedAtUtc = DateTime.UtcNow
        };
        await Suppliers.AddAsync(s, ct);
        await _uow.SaveChangesAsync(ct);
        await _audit.RecordAsync("Supplier.Created", nameof(Supplier), s.SupplierId.ToString(), new { s.Name }, ct);
        return s;
    }

    public async Task<bool> UpdateSupplierAsync(int id, SupplierInput input, CancellationToken ct = default)
    {
        var s = await Suppliers.FirstOrDefaultAsync(x => x.SupplierId == id, ct);
        if (s is null) return false;
        s.Name = input.Name.Trim();
        s.ContactName = input.ContactName; s.ContactNo = input.ContactNo;
        s.Email = input.Email; s.Address = input.Address; s.Notes = input.Notes;
        s.IsActive = input.IsActive;
        await _uow.SaveChangesAsync(ct);
        await _audit.RecordAsync("Supplier.Updated", nameof(Supplier), s.SupplierId.ToString(), new { s.Name }, ct);
        return true;
    }

    // ─── Purchase orders ──────────────────────────────────────────────────

    public async Task<IReadOnlyList<PoSummary>> ListPurchaseOrdersAsync(PurchaseOrderStatus? status, CancellationToken ct = default)
    {
        var q = Pos.NoTrackingQuery().Include(p => p.Supplier).Include(p => p.Lines);
        var filtered = status is null
            ? q.AsQueryable()
            : q.Where(p => p.Status == status);
        return await filtered
            .OrderByDescending(p => p.OrderedAtUtc)
            .Select(p => new PoSummary(
                p.PurchaseOrderId, p.PoNumber, p.SupplierId,
                p.Supplier!.Name, p.OrderedAtUtc, p.ReceivedAtUtc,
                p.Status, p.Lines.Count, p.Lines.Sum(l => l.LineTotal)))
            .ToListAsync(ct);
    }

    public Task<PurchaseOrder?> GetPurchaseOrderAsync(int id, CancellationToken ct = default)
        => Pos.Query().Include(p => p.Supplier).Include(p => p.Lines).ThenInclude(l => l.FoodItem)
              .FirstOrDefaultAsync(p => p.PurchaseOrderId == id, ct);

    public async Task<PurchaseOrder> CreatePurchaseOrderAsync(PoCreateInput input, string? performedBy, CancellationToken ct = default)
    {
        if (input.Lines.Count == 0)
            throw new InvalidOperationException("A purchase order needs at least one line.");

        var po = new PurchaseOrder
        {
            PoNumber = string.IsNullOrWhiteSpace(input.PoNumber) ? $"PO-{DateTime.UtcNow:yyMMddHHmmss}" : input.PoNumber.Trim(),
            SupplierId = input.SupplierId,
            OrderedAtUtc = DateTime.UtcNow,
            Status = PurchaseOrderStatus.Submitted,
            Notes = input.Notes,
            Lines = input.Lines.Select(l => new PurchaseOrderLine
            {
                FoodItemID = l.FoodItemId,
                Quantity = l.Quantity,
                UnitCost = l.UnitCost,
                LineTotal = l.Quantity * l.UnitCost
            }).ToList()
        };
        await Pos.AddAsync(po, ct);
        await _uow.SaveChangesAsync(ct);
        await _audit.RecordAsync("PurchaseOrder.Submitted", nameof(PurchaseOrder), po.PurchaseOrderId.ToString(),
            new { po.PoNumber, po.SupplierId, lines = po.Lines.Count, total = po.Lines.Sum(l => l.LineTotal) }, ct);
        return po;
    }

    public async Task<bool> MarkPurchaseOrderReceivedAsync(int id, string? performedBy, CancellationToken ct = default)
    {
        var po = await Pos.Query().Include(p => p.Lines).FirstOrDefaultAsync(p => p.PurchaseOrderId == id, ct);
        if (po is null || po.Status != PurchaseOrderStatus.Submitted) return false;

        // Bump today's DailyMenu.AvailableQuantity for each line where a row exists for today.
        var today = DateTime.Today;
        var foodIds = po.Lines.Select(l => l.FoodItemID).Distinct().ToList();
        var menus = await DailyMenus.Query()
            .Where(dm => dm.MenuDate.Date == today && foodIds.Contains(dm.FoodItemID))
            .ToListAsync(ct);
        foreach (var l in po.Lines)
        {
            var menu = menus.FirstOrDefault(m => m.FoodItemID == l.FoodItemID);
            if (menu is null) continue;
            menu.AvailableQuantity += l.Quantity;
            menu.InitialQuantity   += l.Quantity;
            menu.IsAvailable = menu.AvailableQuantity > 0;
        }

        po.Status = PurchaseOrderStatus.Received;
        po.ReceivedAtUtc = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);
        await _audit.RecordAsync("PurchaseOrder.Received", nameof(PurchaseOrder), po.PurchaseOrderId.ToString(),
            new { po.PoNumber, performedBy }, ct);
        return true;
    }

    public async Task<bool> CancelPurchaseOrderAsync(int id, string? performedBy, CancellationToken ct = default)
    {
        var po = await Pos.FirstOrDefaultAsync(p => p.PurchaseOrderId == id, ct);
        if (po is null || po.Status == PurchaseOrderStatus.Received) return false;
        po.Status = PurchaseOrderStatus.Cancelled;
        await _uow.SaveChangesAsync(ct);
        await _audit.RecordAsync("PurchaseOrder.Cancelled", nameof(PurchaseOrder), po.PurchaseOrderId.ToString(),
            new { po.PoNumber, performedBy }, ct);
        return true;
    }

    // ─── Stock takes ──────────────────────────────────────────────────────

    public async Task<IReadOnlyList<StockTakeSummary>> ListStockTakesAsync(CancellationToken ct = default)
        => await StockTakes.NoTrackingQuery()
            .Include(s => s.Lines)
            .OrderByDescending(s => s.StartedAtUtc)
            .Select(s => new StockTakeSummary(
                s.StockTakeId, s.StartedAtUtc, s.CompletedAtUtc, s.Status,
                s.Lines.Count, s.Lines.Sum(l => l.CountedQty - l.ExpectedQty)))
            .ToListAsync(ct);

    public Task<StockTake?> GetStockTakeAsync(int id, CancellationToken ct = default)
        => StockTakes.Query().Include(s => s.Lines).ThenInclude(l => l.FoodItem)
                     .FirstOrDefaultAsync(s => s.StockTakeId == id, ct);

    public async Task<StockTake> BeginStockTakeAsync(string? performedBy, CancellationToken ct = default)
    {
        var foods = await _foods.NoTrackingQuery().Where(f => f.IsActive).ToListAsync(ct);
        var today = DateTime.Today;
        var todayMenus = await DailyMenus.NoTrackingQuery()
            .Where(dm => dm.MenuDate.Date == today)
            .ToDictionaryAsync(dm => dm.FoodItemID, dm => dm.AvailableQuantity, ct);

        var take = new StockTake
        {
            StartedAtUtc = DateTime.UtcNow,
            StartedBy = performedBy,
            Status = StockTakeStatus.Draft,
            Lines = foods.Select(f => new StockTakeLine
            {
                FoodItemID = f.FoodItemID,
                ExpectedQty = todayMenus.GetValueOrDefault(f.FoodItemID, 0),
                CountedQty = 0
            }).ToList()
        };
        await StockTakes.AddAsync(take, ct);
        await _uow.SaveChangesAsync(ct);
        await _audit.RecordAsync("StockTake.Started", nameof(StockTake), take.StockTakeId.ToString(),
            new { items = take.Lines.Count, performedBy }, ct);
        return take;
    }

    public async Task<bool> SubmitStockTakeAsync(int id, IReadOnlyList<StockTakeLineInput> counts, string? performedBy, CancellationToken ct = default)
    {
        var take = await StockTakes.Query().Include(s => s.Lines).FirstOrDefaultAsync(s => s.StockTakeId == id, ct);
        if (take is null || take.Status != StockTakeStatus.Draft) return false;

        var countMap = counts.ToDictionary(c => c.FoodItemId, c => c.CountedQty);
        foreach (var line in take.Lines)
        {
            if (countMap.TryGetValue(line.FoodItemID, out var counted))
                line.CountedQty = counted;
        }
        take.Status = StockTakeStatus.Submitted;
        take.CompletedAtUtc = DateTime.UtcNow;
        take.SubmittedBy = performedBy;
        await _uow.SaveChangesAsync(ct);
        await _audit.RecordAsync("StockTake.Submitted", nameof(StockTake), take.StockTakeId.ToString(),
            new { variance = take.Lines.Sum(l => l.CountedQty - l.ExpectedQty), performedBy }, ct);
        return true;
    }

    public async Task<bool> CancelStockTakeAsync(int id, string? performedBy, CancellationToken ct = default)
    {
        var take = await StockTakes.FirstOrDefaultAsync(s => s.StockTakeId == id, ct);
        if (take is null || take.Status != StockTakeStatus.Draft) return false;
        take.Status = StockTakeStatus.Cancelled;
        take.CompletedAtUtc = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);
        return true;
    }

    // ─── Waste log ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<WasteLogRow>> ListWasteAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        IQueryable<WasteLog> q = Waste.NoTrackingQuery().Include(w => w.FoodItem);
        if (from.HasValue) q = q.Where(w => w.OccurredAtUtc >= from.Value);
        if (to.HasValue)   q = q.Where(w => w.OccurredAtUtc < to.Value.AddDays(1));
        return await q.OrderByDescending(w => w.OccurredAtUtc)
            .Select(w => new WasteLogRow(
                w.WasteLogId, w.FoodItemID, w.FoodItem!.ItemName,
                w.Quantity, w.Reason, w.Notes, w.PerformedBy, w.OccurredAtUtc))
            .ToListAsync(ct);
    }

    public async Task<WasteLog> RecordWasteAsync(WasteLogInput input, string? performedBy, CancellationToken ct = default)
    {
        var row = new WasteLog
        {
            FoodItemID = input.FoodItemId,
            Quantity = input.Quantity,
            Reason = input.Reason,
            Notes = input.Notes,
            PerformedBy = performedBy,
            OccurredAtUtc = DateTime.UtcNow
        };
        await Waste.AddAsync(row, ct);

        // Reduce today's DailyMenu.AvailableQuantity (clamped at zero).
        var today = DateTime.Today;
        var menu = await DailyMenus.Query()
            .Where(dm => dm.MenuDate.Date == today && dm.FoodItemID == input.FoodItemId)
            .FirstOrDefaultAsync(ct);
        if (menu is not null)
        {
            menu.AvailableQuantity = Math.Max(0, menu.AvailableQuantity - input.Quantity);
            if (menu.AvailableQuantity <= 0) menu.IsAvailable = false;
        }

        await _uow.SaveChangesAsync(ct);
        await _audit.RecordAsync("Waste.Recorded", nameof(WasteLog), row.WasteLogId.ToString(),
            new { input.FoodItemId, input.Quantity, reason = input.Reason.ToString() }, ct);
        return row;
    }

    // ─── Near-expiry ──────────────────────────────────────────────────────
    public async Task<IReadOnlyList<NearExpiryRow>> ListNearExpiryAsync(int hoursWindow, CancellationToken ct = default)
    {
        // Look at today + tomorrow's menu. ExpiresAtUtc = MenuDate.Date + ShelfLifeHours.
        var today    = DateTime.UtcNow.Date;
        var horizon  = today.AddDays(2);
        var nowUtc   = DateTime.UtcNow;

        // Pull rows then project + filter in memory — ShelfLifeHours is nullable on FoodItem
        // and the expiry maths is server-translation-fragile; the result set is tiny
        // (1-2 menu days × ~30 items) so this is well within budget.
        var rows = await DailyMenus.Query()
            .Where(dm => dm.MenuDate >= today && dm.MenuDate < horizon
                         && dm.AvailableQuantity > 0
                         && dm.FoodItem != null
                         && dm.FoodItem.ShelfLifeHours != null)
            .Select(dm => new
            {
                dm.DailyMenuID,
                dm.FoodItemID,
                ItemName = dm.FoodItem!.ItemName,
                dm.MenuDate,
                ShelfLifeHours = dm.FoodItem!.ShelfLifeHours!.Value,
                dm.AvailableQuantity,
                Price = dm.FoodItem!.Price
            })
            .ToListAsync(ct);

        var windowCutoff = nowUtc.AddHours(hoursWindow);
        return rows
            .Select(r =>
            {
                var expires = r.MenuDate.Date.AddHours(r.ShelfLifeHours);
                return new NearExpiryRow(
                    r.DailyMenuID, r.FoodItemID, r.ItemName,
                    r.MenuDate, r.ShelfLifeHours, expires,
                    r.AvailableQuantity, r.Price,
                    (expires - nowUtc).TotalHours);
            })
            .Where(r => r.ExpiresAtUtc <= windowCutoff)   // already expired OR expiring within window
            .OrderBy(r => r.ExpiresAtUtc)
            .ToList();
    }
}
