// =============================================================================
// PreOrderController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Per ADR 0004 the controller depends on IPreOrderService + IDispatcher — no
// IAppDbContext.
// =============================================================================

using System.Security.Claims;
using CanteenManagementSystem.Application.Orders;
using CanteenManagementSystem.Application.Orders.Commands;
using CanteenManagementSystem.Application.Orders.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Dispatch;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "AuthenticatedAny")]
[Route("pre-orders")]
public sealed class PreOrderController : Controller
{
    private readonly IPreOrderService _preOrders;
    private readonly IDispatcher _dispatcher;

    public PreOrderController(IPreOrderService preOrders, IDispatcher dispatcher)
    {
        _preOrders = preOrders; _dispatcher = dispatcher;
    }

    public sealed record OrderRow(int OrderId, string OrderNumber, DateTime PickupAtUtc,
        CanteenManagementSystem.Domain.Enums.CanteenOrderStatus Status, decimal TotalAmount,
        IReadOnlyList<string> Items);

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var uid = ResolveUserId();
        if (uid is null) return View(Array.Empty<OrderRow>());
        var linkedId = await _preOrders.ResolveLinkedPersonIdAsync(uid.Value, ct);
        if (string.IsNullOrEmpty(linkedId)) return View(Array.Empty<OrderRow>());
        var rows = await _preOrders.ListMyUpcomingAsync(linkedId, ct);
        return View(rows.Select(r =>
            new OrderRow(r.OrderId, r.OrderNumber, r.PickupAtUtc, r.Status, r.TotalAmount, r.Items)).ToList());
    }

    public sealed class NewForm
    {
        public DateTime PickupDate { get; set; } = DateTime.UtcNow.Date.AddDays(1);
        public List<int> SelectedDailyMenuIds { get; set; } = new();
        public List<int> Quantities { get; set; } = new();
    }

    [HttpGet("new")]
    public async Task<IActionResult> New(DateTime? date, CancellationToken ct)
    {
        var target = (date ?? DateTime.UtcNow.Date.AddDays(1)).Date;
        if (target < DateTime.UtcNow.Date) target = DateTime.UtcNow.Date;
        if (target > DateTime.UtcNow.Date.AddDays(14)) target = DateTime.UtcNow.Date.AddDays(14);

        ViewBag.Target = target;
        ViewBag.Menu   = await _preOrders.GetMenuForDateAsync(target, ct);
        return View(new NewForm { PickupDate = target });
    }

    [HttpPost("new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> New(NewForm form, CancellationToken ct)
    {
        var uid = ResolveUserId();
        if (uid is null) return RedirectToAction(nameof(Index));

        var linkedId = await _preOrders.ResolveLinkedPersonIdAsync(uid.Value, ct);
        if (string.IsNullOrEmpty(linkedId))
        {
            TempData["Flash.Error"] = "Your account isn't linked to a student/employee record — ask the admin to set LinkedPersonId.";
            return RedirectToAction(nameof(Index));
        }

        var pickup = form.PickupDate.Date;
        if (pickup < DateTime.UtcNow.Date || pickup > DateTime.UtcNow.Date.AddDays(14))
        {
            TempData["Flash.Error"] = "Pickup date must be within the next 14 days.";
            return RedirectToAction(nameof(New), new { date = pickup });
        }

        var items = await _preOrders.ResolveLineItemsAsync(form.SelectedDailyMenuIds, form.Quantities, ct);
        if (items.Count == 0)
        {
            TempData["Flash.Error"] = "Add at least one item.";
            return RedirectToAction(nameof(New), new { date = pickup });
        }

        var userType = await _preOrders.ResolveUserTypeAsync(linkedId, ct);
        var idemKey = $"preorder:{uid}:{pickup:yyyyMMdd}";

        var result = await _dispatcher.SendAsync(new PlaceOrderCommand(new PlaceOrderRequestDto
        {
            UserId         = linkedId,
            UserIdentifier = linkedId,
            UserType       = userType,
            IdempotencyKey = idemKey,
            Items          = items.ToList()
        }), ct);

        if (!result.Success)
        {
            TempData["Flash.Error"] = result.Message ?? "Could not place the pre-order.";
            return RedirectToAction(nameof(New), new { date = pickup });
        }

        await _preOrders.StampPreOrderMetadataAsync(result.OrderId, pickup, ct);

        TempData["Flash.Success"] = $"Pre-order #{result.OrderNumber} confirmed for {pickup:dd MMM yyyy}.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Operator's view of today's pre-orders waiting for pickup.</summary>
    [HttpGet("/admin/pre-orders/queue")]
    [Authorize(Policy = "Operator")]
    public async Task<IActionResult> Queue(CancellationToken ct)
        => View("Queue", await _preOrders.GetTodayPickupQueueAsync(ct));

    private int? ResolveUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var n) ? n : null;
    }
}
