// =============================================================================
// KitchenController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// TV-friendly Kitchen Display Screen (KDS). Consumes the existing
// KitchenDisplayHub SignalR feed:
//   "order:placed"   → new card appears in the queue
//   "order:status"   → card updates / moves columns / disappears
//
// SERVER STATE
//   Initial page renders TODAY'S Pending + Preparing + Ready orders straight
//   from OperatorQueryService — so a refresh recovers cleanly without losing
//   anything. Live edits happen via SignalR + a tiny REST endpoint
//   (POST /Kitchen/Status) that mirrors the operator dashboard's action.
// =============================================================================

using CanteenManagementSystem.Application.Operators;
using CanteenManagementSystem.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "Operator")]
[Route("Kitchen")]
public sealed class KitchenController : Controller
{
    private readonly IOperatorQueryService _ops;
    public KitchenController(IOperatorQueryService ops) => _ops = ops;

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        // Reuses the existing operator query for today's pending orders;
        // the client-side script promotes / demotes cards as SignalR fires.
        var pending = await _ops.GetPendingOrdersAsync(DateTime.Today, page: 1, pageSize: 100);
        return View(pending);
    }

    [HttpPost("status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Status([FromForm] int orderId, [FromForm] int status)
    {
        // Permit Preparing / Ready / Delivered transitions from the kitchen.
        if (status != (int)CanteenOrderStatus.Preparing &&
            status != (int)CanteenOrderStatus.Ready &&
            status != (int)CanteenOrderStatus.Delivered)
        {
            return BadRequest(new { error = "Invalid status for KDS." });
        }

        var result = await _ops.UpdateOrderStatusAsync(orderId, status);
        return Json(result);
    }
}
