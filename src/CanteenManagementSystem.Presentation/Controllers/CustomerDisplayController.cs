// =============================================================================
// CustomerDisplayController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Anonymous public route that surfaces a single order's status — the
// "customer-facing display" pole-screen at the counter. The screen shows:
//   * order number (big),
//   * line items + total,
//   * current status (Pending / Confirmed / Preparing / Ready / Delivered)
//   * live SignalR updates via OrderNotificationHub.
//
// SECURITY
//   Read-only, no PII beyond the OrderNumber + items + status. The route is
//   indexed by OrderNumber (not OrderId) so a casual browser can't enumerate.
// =============================================================================

using CanteenManagementSystem.Domain.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Persistence;

namespace CanteenManagementSystem.Presentation.Controllers;

[AllowAnonymous]
[Route("display/order")]
public sealed class CustomerDisplayController : Controller
{
    private readonly IReadOnlyRepository<Order> _orders;

    public CustomerDisplayController(IReadOnlyRepository<Order> orders) { _orders = orders; }

    public sealed record CustomerDisplayItem(string ItemName, int Quantity, decimal LineTotal);
    public sealed record CustomerDisplayVm(
        string OrderNumber, string Status, decimal TotalAmount,
        IReadOnlyList<CustomerDisplayItem> Items, DateTime OrderDate);

    [HttpGet("{orderNumber}")]
    public async Task<IActionResult> Show(string orderNumber, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(orderNumber)) return NotFound();
        var trimmed = orderNumber.Trim();

        var order = await _orders.NoTrackingQuery()
            .Where(o => o.OrderNumber == trimmed)
            .Include(o => o.OrderItems).ThenInclude(oi => oi.FoodItem)
            .FirstOrDefaultAsync(ct);
        if (order is null) return NotFound();

        return View(new CustomerDisplayVm(
            order.OrderNumber,
            order.Status.ToString(),
            order.TotalAmount,
            order.OrderItems.Select(oi => new CustomerDisplayItem(
                oi.FoodItem?.ItemName ?? "—",
                oi.Quantity,
                oi.TotalPrice)).ToList(),
            order.OrderDate));
    }
}
