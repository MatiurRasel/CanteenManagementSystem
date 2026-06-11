// =============================================================================
// TableOrderController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// QR-on-table consumer flow. Per ADR 0004 the controller depends on
// ITableOrderService + IDispatcher — no IAppDbContext.
// =============================================================================

using CanteenManagementSystem.Application.Orders;
using CanteenManagementSystem.Application.Orders.Commands;
using CanteenManagementSystem.Application.Orders.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Dispatch;

namespace CanteenManagementSystem.Presentation.Controllers;

[AllowAnonymous]
[Route("t")]
public sealed class TableOrderController : Controller
{
    private readonly ITableOrderService _tableOrders;
    private readonly IDispatcher _dispatcher;

    public TableOrderController(ITableOrderService tableOrders, IDispatcher dispatcher)
    {
        _tableOrders = tableOrders; _dispatcher = dispatcher;
    }

    public sealed record PageVm(string TableNumber, DateTime Date, IReadOnlyList<TableMenuItem> Items);

    [HttpGet("{tableNumber}")]
    public async Task<IActionResult> Index(string tableNumber, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var menu = await _tableOrders.GetTodayMenuAsync(today, ct);
        return View(new PageVm(tableNumber, today, menu));
    }

    public sealed class PlaceForm
    {
        public string Identifier { get; set; } = string.Empty;
        public List<int> DailyMenuIds { get; set; } = new();
        public List<int> Quantities { get; set; } = new();
    }

    [HttpPost("{tableNumber}/place")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Place(string tableNumber, PlaceForm form, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(form.Identifier))
        {
            TempData["Flash.Error"] = "Please enter your ID or card UID.";
            return RedirectToAction(nameof(Index), new { tableNumber });
        }

        var resolved = await _tableOrders.ResolveUserAsync(form.Identifier, ct);
        if (resolved is null)
        {
            TempData["Flash.Error"] = "User not found. Ask the canteen counter to verify your ID.";
            return RedirectToAction(nameof(Index), new { tableNumber });
        }

        var items = await _tableOrders.ResolveLineItemsAsync(form.DailyMenuIds, form.Quantities, ct);
        if (items.Count == 0)
        {
            TempData["Flash.Error"] = "Add at least one item.";
            return RedirectToAction(nameof(Index), new { tableNumber });
        }

        var idemKey = $"table:{tableNumber}:{resolved.ExternalId}:{DateTime.UtcNow:yyyyMMddHHmm}";
        var result = await _dispatcher.SendAsync(new PlaceOrderCommand(new PlaceOrderRequestDto
        {
            UserId         = resolved.ExternalId,
            UserIdentifier = resolved.ExternalId,
            UserType       = resolved.UserType,
            IdempotencyKey = idemKey,
            Items          = items.ToList()
        }), ct);

        if (!result.Success)
        {
            TempData["Flash.Error"] = result.Message ?? "Could not place the order.";
            return RedirectToAction(nameof(Index), new { tableNumber });
        }

        await _tableOrders.StampTableNumberAsync(result.OrderId, tableNumber, ct);

        TempData["Flash.Success"] = $"Order #{result.OrderNumber} placed — pick up from the counter when ready.";
        return RedirectToAction(nameof(Index), new { tableNumber });
    }
}
