using Platform.Application.Dispatch;
using CanteenManagementSystem.Application.Operators;
using CanteenManagementSystem.Application.Orders.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "Operator")]
public class OperatorController : Controller
{
    private readonly IOperatorQueryService _operatorQueryService;
    private readonly IDispatcher _dispatcher;

    public OperatorController(IOperatorQueryService operatorQueryService, IDispatcher dispatcher)
    {
        _operatorQueryService = operatorQueryService;
        _dispatcher = dispatcher;
    }

    // PRG: keep ?selectedDate=... out of the URL. Session remembers the
    // operator's last-picked filter. GET accepts an optional `selectedDate`
    // query string for back-compat with old bookmarks (it stashes into session
    // and redirects to a clean URL). POST uses a distinct C# name + [ActionName]
    // so MVC's action selector never mis-routes a legacy GET to the POST overload.
    private const string SessionKey_OperatorDashDate = "Operator.Dashboard.SelectedDate";

    [HttpGet]
    public async Task<IActionResult> Dashboard(DateTime? selectedDate)
    {
        if (selectedDate.HasValue)
        {
            HttpContext.Session.SetString(SessionKey_OperatorDashDate, selectedDate.Value.ToString("yyyy-MM-dd"));
            return RedirectToAction(nameof(Dashboard));
        }
        var raw = HttpContext.Session.GetString(SessionKey_OperatorDashDate);
        DateTime? saved = DateTime.TryParse(raw, out var d) ? d : null;
        ViewBag.Stats = await _operatorQueryService.GetDashboardStatsAsync(saved);
        ViewBag.SelectedDate = saved ?? DateTime.Today;
        return View();
    }

    [HttpPost]
    [ActionName(nameof(Dashboard))]
    [ValidateAntiForgeryToken]
    public IActionResult DashboardPost(DateTime selectedDate)
    {
        HttpContext.Session.SetString(SessionKey_OperatorDashDate, selectedDate.ToString("yyyy-MM-dd"));
        return RedirectToAction(nameof(Dashboard));
    }

    public async Task<IActionResult> GetPendingOrders(DateTime? selectedDate, int page = 1, int pageSize = 50)
        => Json(await _operatorQueryService.GetPendingOrdersAsync(selectedDate, page, pageSize));

    // Delivery goes through the command pipeline so wallet deduction, inventory
    // consume, and audit trail all run inside the dispatcher transaction.
    [HttpPost]
    public async Task<IActionResult> MarkAsDelivered([FromBody] DeliverOrderRequest request, CancellationToken cancellationToken)
        => Json(await _dispatcher.SendAsync(new MarkOrderDeliveredCommand(request.OrderId), cancellationToken));

    // Void / Refund — single endpoint, semantics chosen by handler based on
    // current order status (Cancelled for not-yet-delivered, Refunded for delivered).
    // Returns JSON so dashboard/KDS can pick up the result via fetch.
    [HttpPost]
    public async Task<IActionResult> Void([FromBody] VoidOrderRequest request, CancellationToken cancellationToken)
        => Json(await _dispatcher.SendAsync(new VoidOrderCommand(request.OrderId, request.Reason, request.Amount), cancellationToken));

    [HttpGet]
    public async Task<IActionResult> SearchAllOrders(string search, DateTime? selectedDate, int page = 1, int pageSize = 12)
        => Json(await _operatorQueryService.SearchAllOrdersAsync(search, selectedDate, page, pageSize));

    [HttpGet]
    public async Task<IActionResult> SearchOrders(string search, DateTime? selectedDate, int page = 1, int pageSize = 50)
        => Json(await _operatorQueryService.SearchOrdersAsync(search, selectedDate, page, pageSize));

    public async Task<IActionResult> History(DateTime? date)
    {
        ViewBag.TargetDate = date ?? DateTime.Today;
        var orders = await _operatorQueryService.GetHistoryOrdersAsync(date);
        return View(orders);
    }

    [HttpGet]
    public async Task<IActionResult> GetStatistics(DateTime? selectedDate)
        => Json(await _operatorQueryService.GetStatisticsAsync(selectedDate));

    // End-of-shift summary: totals, top items, cancellations. Printable.
    public async Task<IActionResult> ShiftSummary(DateTime? date, CancellationToken ct)
    {
        var summary = await _operatorQueryService.GetShiftSummaryAsync((date ?? DateTime.Today).Date, ct);
        return View(summary);
    }

    // Today's orders as a CSV (audit / accounting export).
    public async Task<IActionResult> ExportOrdersCsv(DateTime? date, CancellationToken ct)
    {
        var d = (date ?? DateTime.Today).Date;
        var orders = await _operatorQueryService.GetHistoryOrdersAsync(d);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("OrderNumber,Status,UserId,UserType,TotalAmount,OrderDate,DeliveredDate");
        foreach (var o in orders)
        {
            sb.Append(o.OrderNumber).Append(',')
              .Append(o.Status).Append(',')
              .Append(CsvEscape(o.UserId)).Append(',')
              .Append(o.UserType).Append(',')
              .Append(o.TotalAmount.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
              .Append(o.OrderDate.ToString("o")).Append(',')
              .Append(o.DeliveredDate?.ToString("o") ?? string.Empty)
              .AppendLine();
        }
        var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"orders_{d:yyyyMMdd}.csv");
    }

    public async Task<IActionResult> Orders(int status = -1)
    {
        ViewBag.Status = status;
        var orders = await _operatorQueryService.GetOrdersAsync(status);
        return View(orders);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateOrderStatus(int orderId, int status)
        => Json(await _operatorQueryService.UpdateOrderStatusAsync(orderId, status));

    public async Task<IActionResult> GetLiveOrders()
        => Json(await _operatorQueryService.GetLiveOrdersAsync());

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}

public class DeliverOrderRequest
{
    public int OrderId { get; set; }
}

public class VoidOrderRequest
{
    public int OrderId { get; set; }
    public string? Reason { get; set; }

    /// <summary>
    /// Optional partial-refund amount. Null = full refund (current behaviour).
    /// Only honoured when the order is already <c>Delivered</c> — pre-delivery
    /// voids always release the full block.
    /// </summary>
    public decimal? Amount { get; set; }
}
