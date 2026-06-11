using Platform.Application.Dispatch;
using CanteenManagementSystem.Application.Orders.Commands;
using CanteenManagementSystem.Application.Orders.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Presentation.Controllers;

public class OrderController : Controller
{
    private readonly IDispatcher _dispatcher;

    public OrderController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpPost]
    public async Task<IActionResult> PlaceOrder([FromBody] PlaceOrderRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.IdempotencyKey) && Request.Headers.TryGetValue("X-Idempotency-Key", out var headerKey))
        {
            request.IdempotencyKey = headerKey.ToString();
        }

        var result = await _dispatcher.SendAsync(new PlaceOrderCommand(request), cancellationToken);
        return Json(result);
    }
}
