// =============================================================================
// ReceiptSearchController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Admin-facing receipt finder. Search by order number, user id, or date range;
// click through to re-print or download the PDF/HTML receipt.
//   GET /admin/receipts/search
// =============================================================================

using CanteenManagementSystem.Domain.Enums;
using CanteenManagementSystem.Domain.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Persistence;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "Operator")]
[Route("admin/receipts")]
public sealed class ReceiptSearchController : Controller
{
    private readonly IReadOnlyRepository<Order> _orders;

    public ReceiptSearchController(IReadOnlyRepository<Order> orders) { _orders = orders; }

    public sealed record ReceiptHit(
        int OrderId, string OrderNumber, string UserId, CanteenUserType UserType,
        DateTime OrderDate, decimal TotalAmount, CanteenOrderStatus Status);

    public sealed record ReceiptSearchVm(
        string? Q, DateTime? From, DateTime? To,
        IReadOnlyList<ReceiptHit> Hits, int TotalCount, int Page, int PageSize);

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        string? q, DateTime? from, DateTime? to,
        int page = 1, int pageSize = 50,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 200);

        var query = _orders.NoTrackingQuery();

        if (from.HasValue) query = query.Where(o => o.OrderDate >= from.Value);
        if (to.HasValue)   query = query.Where(o => o.OrderDate < to.Value.AddDays(1));
        if (!string.IsNullOrWhiteSpace(q))
        {
            var needle = q.Trim();
            query = query.Where(o =>
                o.OrderNumber.Contains(needle) ||
                o.UserId.Contains(needle));
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(o => o.OrderDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new ReceiptHit(
                o.OrderID, o.OrderNumber, o.UserId, o.UserType,
                o.OrderDate, o.TotalAmount, o.Status))
            .ToListAsync(ct);

        return View(new ReceiptSearchVm(q, from, to, rows, total, page, pageSize));
    }
}
