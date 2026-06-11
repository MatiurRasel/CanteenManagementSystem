// =============================================================================
// IInventoryAdminService  (CanteenManagementSystem.Application.Inventory)
// -----------------------------------------------------------------------------
// Aggregates all three "operate the storeroom" admin flows behind a single
// service surface — Suppliers, Purchase Orders, Stock Takes, Waste Log.
// Keeps controllers thin (one service ref per controller).
// =============================================================================

using CanteenManagementSystem.Domain.Inventory;

namespace CanteenManagementSystem.Application.Inventory;

// ─── Suppliers ─────────────────────────────────────────────────────────────
public sealed record SupplierInput(
    string Name, string? ContactName, string? ContactNo, string? Email,
    string? Address, string? Notes, bool IsActive);

// ─── Purchase orders ───────────────────────────────────────────────────────
public sealed record PoLineInput(int FoodItemId, int Quantity, decimal UnitCost);
public sealed record PoCreateInput(int SupplierId, string PoNumber, string? Notes, IReadOnlyList<PoLineInput> Lines);

public sealed record PoSummary(
    int PurchaseOrderId, string PoNumber, int SupplierId, string SupplierName,
    DateTime OrderedAtUtc, DateTime? ReceivedAtUtc, PurchaseOrderStatus Status,
    int LineCount, decimal Total);

// ─── Stock takes ───────────────────────────────────────────────────────────
public sealed record StockTakeLineInput(int FoodItemId, int CountedQty);

public sealed record StockTakeSummary(
    int StockTakeId, DateTime StartedAtUtc, DateTime? CompletedAtUtc,
    StockTakeStatus Status, int LineCount, int TotalVariance);

// ─── Waste ─────────────────────────────────────────────────────────────────
public sealed record WasteLogInput(int FoodItemId, int Quantity, WasteReason Reason, string? Notes);

public sealed record WasteLogRow(
    long WasteLogId, int FoodItemId, string ItemName, int Quantity,
    WasteReason Reason, string? Notes, string? PerformedBy, DateTime OccurredAtUtc);

// ─── Near-expiry ───────────────────────────────────────────────────────────
public sealed record NearExpiryRow(
    int DailyMenuId,
    int FoodItemId,
    string ItemName,
    DateTime MenuDate,
    int ShelfLifeHours,
    DateTime ExpiresAtUtc,
    int AvailableQuantity,
    decimal Price,
    /// <summary>Hours until expiry. Negative = already expired.</summary>
    double HoursToExpiry);

public interface IInventoryAdminService
{
    // Suppliers
    Task<IReadOnlyList<Supplier>>   ListSuppliersAsync(bool includeInactive, CancellationToken ct = default);
    Task<Supplier?>                 GetSupplierAsync(int id, CancellationToken ct = default);
    Task<Supplier>                  CreateSupplierAsync(SupplierInput input, CancellationToken ct = default);
    Task<bool>                      UpdateSupplierAsync(int id, SupplierInput input, CancellationToken ct = default);

    // Purchase orders
    Task<IReadOnlyList<PoSummary>>  ListPurchaseOrdersAsync(PurchaseOrderStatus? status, CancellationToken ct = default);
    Task<PurchaseOrder?>            GetPurchaseOrderAsync(int id, CancellationToken ct = default);
    Task<PurchaseOrder>             CreatePurchaseOrderAsync(PoCreateInput input, string? performedBy, CancellationToken ct = default);
    Task<bool>                      MarkPurchaseOrderReceivedAsync(int id, string? performedBy, CancellationToken ct = default);
    Task<bool>                      CancelPurchaseOrderAsync(int id, string? performedBy, CancellationToken ct = default);

    // Stock takes
    Task<IReadOnlyList<StockTakeSummary>> ListStockTakesAsync(CancellationToken ct = default);
    Task<StockTake?>                GetStockTakeAsync(int id, CancellationToken ct = default);
    Task<StockTake>                 BeginStockTakeAsync(string? performedBy, CancellationToken ct = default);
    Task<bool>                      SubmitStockTakeAsync(int id, IReadOnlyList<StockTakeLineInput> counts, string? performedBy, CancellationToken ct = default);
    Task<bool>                      CancelStockTakeAsync(int id, string? performedBy, CancellationToken ct = default);

    // Waste log
    Task<IReadOnlyList<WasteLogRow>> ListWasteAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<WasteLog>                  RecordWasteAsync(WasteLogInput input, string? performedBy, CancellationToken ct = default);

    /// <summary>
    /// Items in today/tomorrow's menu whose shelf-life is about to expire (or already has).
    /// Only items with ShelfLifeHours set + AvailableQuantity > 0 are returned.
    /// </summary>
    Task<IReadOnlyList<NearExpiryRow>> ListNearExpiryAsync(int hoursWindow, CancellationToken ct = default);
}
