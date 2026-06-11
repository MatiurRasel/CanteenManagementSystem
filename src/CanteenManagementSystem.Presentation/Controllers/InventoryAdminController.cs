// =============================================================================
// InventoryAdminController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// One controller fans out into the three storeroom flows so the sidebar holds
// a single "Inventory" entry. JSON-friendly endpoints are exposed for the
// tablet-on-shelf stock-take experience.
// =============================================================================

using CanteenManagementSystem.Application.Inventory;
using CanteenManagementSystem.Application.Menus;
using CanteenManagementSystem.Domain.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "TenantAdmin")]
[Route("admin/inventory")]
public sealed class InventoryAdminController : Controller
{
    private readonly IInventoryAdminService _svc;
    private readonly IMenuManagementService _menus;

    public InventoryAdminController(IInventoryAdminService svc, IMenuManagementService menus)
    {
        _svc = svc; _menus = menus;
    }

    private string? Me() => User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

    // ─── Suppliers ────────────────────────────────────────────────────────

    [HttpGet("suppliers")]
    public async Task<IActionResult> Suppliers(CancellationToken ct)
        => View(await _svc.ListSuppliersAsync(includeInactive: true, ct));

    [HttpGet("suppliers/edit/{id:int?}")]
    public async Task<IActionResult> EditSupplier(int? id, CancellationToken ct)
    {
        if (id is null) return View(new Supplier());
        var s = await _svc.GetSupplierAsync(id.Value, ct);
        return s is null ? NotFound() : View(s);
    }

    [HttpPost("suppliers/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSupplier(int? supplierId, string name,
        string? contactName, string? contactNo, string? email, string? address, string? notes,
        bool isActive, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Flash.Error"] = "Name is required.";
            return RedirectToAction(nameof(EditSupplier), new { id = supplierId });
        }
        var input = new SupplierInput(name, contactName, contactNo, email, address, notes, isActive);
        if (supplierId is null)
        {
            await _svc.CreateSupplierAsync(input, ct);
            TempData["Flash.Success"] = $"Supplier '{name}' added.";
        }
        else
        {
            var ok = await _svc.UpdateSupplierAsync(supplierId.Value, input, ct);
            if (!ok) return NotFound();
            TempData["Flash.Success"] = $"Supplier '{name}' saved.";
        }
        return RedirectToAction(nameof(Suppliers));
    }

    // ─── Purchase orders ──────────────────────────────────────────────────

    [HttpGet("pos")]
    public async Task<IActionResult> PurchaseOrders(PurchaseOrderStatus? status, CancellationToken ct)
    {
        ViewBag.Status = status;
        return View(await _svc.ListPurchaseOrdersAsync(status, ct));
    }

    [HttpGet("pos/new")]
    public async Task<IActionResult> NewPurchaseOrder(CancellationToken ct)
    {
        ViewBag.Suppliers = await _svc.ListSuppliersAsync(includeInactive: false, ct);
        ViewBag.FoodItems = await _menus.GetManageItemsAsync();
        return View();
    }

    [HttpPost("pos/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePurchaseOrder(
        int supplierId, string? poNumber, string? notes,
        int[] foodItemId, int[] quantity, decimal[] unitCost,
        CancellationToken ct)
    {
        var lines = new List<PoLineInput>();
        for (var i = 0; i < foodItemId.Length && i < quantity.Length && i < unitCost.Length; i++)
        {
            if (foodItemId[i] <= 0 || quantity[i] <= 0) continue;
            lines.Add(new PoLineInput(foodItemId[i], quantity[i], unitCost[i]));
        }
        if (lines.Count == 0)
        {
            TempData["Flash.Error"] = "Add at least one line.";
            return RedirectToAction(nameof(NewPurchaseOrder));
        }

        var po = await _svc.CreatePurchaseOrderAsync(new PoCreateInput(supplierId, poNumber ?? string.Empty, notes, lines), Me(), ct);
        TempData["Flash.Success"] = $"PO {po.PoNumber} submitted.";
        return RedirectToAction(nameof(PurchaseOrders));
    }

    [HttpPost("pos/{id:int}/receive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReceivePurchaseOrder(int id, CancellationToken ct)
    {
        var ok = await _svc.MarkPurchaseOrderReceivedAsync(id, Me(), ct);
        TempData[ok ? "Flash.Success" : "Flash.Error"] = ok
            ? "PO marked received — inventory bumped."
            : "PO already in a terminal state.";
        return RedirectToAction(nameof(PurchaseOrders));
    }

    [HttpPost("pos/{id:int}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelPurchaseOrder(int id, CancellationToken ct)
    {
        var ok = await _svc.CancelPurchaseOrderAsync(id, Me(), ct);
        TempData[ok ? "Flash.Success" : "Flash.Error"] = ok ? "PO cancelled." : "Cannot cancel a received PO.";
        return RedirectToAction(nameof(PurchaseOrders));
    }

    // ─── Stock takes ──────────────────────────────────────────────────────

    [HttpGet("stock-takes")]
    public async Task<IActionResult> StockTakes(CancellationToken ct)
        => View(await _svc.ListStockTakesAsync(ct));

    [HttpPost("stock-takes/begin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BeginStockTake(CancellationToken ct)
    {
        var take = await _svc.BeginStockTakeAsync(Me(), ct);
        return RedirectToAction(nameof(StockTakeDetail), new { id = take.StockTakeId });
    }

    [HttpGet("stock-takes/{id:int}")]
    public async Task<IActionResult> StockTakeDetail(int id, CancellationToken ct)
    {
        var take = await _svc.GetStockTakeAsync(id, ct);
        return take is null ? NotFound() : View(take);
    }

    [HttpPost("stock-takes/{id:int}/submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitStockTake(int id, int[] foodItemId, int[] countedQty, CancellationToken ct)
    {
        var counts = new List<StockTakeLineInput>();
        for (var i = 0; i < foodItemId.Length && i < countedQty.Length; i++)
        {
            counts.Add(new StockTakeLineInput(foodItemId[i], Math.Max(0, countedQty[i])));
        }
        var ok = await _svc.SubmitStockTakeAsync(id, counts, Me(), ct);
        TempData[ok ? "Flash.Success" : "Flash.Error"] = ok ? "Stock take submitted." : "Stock take already submitted.";
        return RedirectToAction(nameof(StockTakes));
    }

    // ─── Near-expiry items ────────────────────────────────────────────────

    [HttpGet("near-expiry")]
    public async Task<IActionResult> NearExpiry(int hours = 8, CancellationToken ct = default)
    {
        if (hours < 1)  hours = 1;
        if (hours > 72) hours = 72;
        ViewBag.HoursWindow = hours;
        var rows = await _svc.ListNearExpiryAsync(hours, ct);
        return View(rows);
    }

    // ─── Waste log ────────────────────────────────────────────────────────

    [HttpGet("waste")]
    public async Task<IActionResult> Waste(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var rows = await _svc.ListWasteAsync(from, to, ct);
        ViewBag.From = from; ViewBag.To = to;
        ViewBag.FoodItems = await _menus.GetManageItemsAsync();
        return View(rows);
    }

    [HttpPost("waste/record")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordWaste(int foodItemId, int quantity, WasteReason reason, string? notes, CancellationToken ct)
    {
        if (foodItemId <= 0 || quantity <= 0)
        {
            TempData["Flash.Error"] = "Food item and a positive quantity are required.";
            return RedirectToAction(nameof(Waste));
        }
        await _svc.RecordWasteAsync(new WasteLogInput(foodItemId, quantity, reason, notes), Me(), ct);
        TempData["Flash.Success"] = "Waste recorded.";
        return RedirectToAction(nameof(Waste));
    }
}
