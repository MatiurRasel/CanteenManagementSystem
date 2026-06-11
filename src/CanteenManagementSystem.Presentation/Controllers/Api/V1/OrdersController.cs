using Asp.Versioning;
using Platform.Application.Dispatch;
using CanteenManagementSystem.Application.Operators;
using CanteenManagementSystem.Application.Orders.Commands;
using CanteenManagementSystem.Application.Orders.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Presentation.Controllers.Api.V1;

/// Order lifecycle endpoints. All writes go through the in-house CQRS
/// dispatcher so wallet block/deduct, inventory reserve/consume, and audit
/// run inside the same transaction.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/orders")]
[Produces("application/json")]
public sealed class OrdersController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly IOperatorQueryService _operatorQueryService;

    public OrdersController(IDispatcher dispatcher, IOperatorQueryService operatorQueryService)
    {
        _dispatcher = dispatcher;
        _operatorQueryService = operatorQueryService;
    }

    /// <summary>Place an order (block wallet + reserve stock).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails),   StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OrderResponseDto>> Place(
        [FromBody] PlaceOrderRequestDto request,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.IdempotencyKey) && !string.IsNullOrEmpty(idempotencyKey))
        {
            request.IdempotencyKey = idempotencyKey;
        }
        var result = await _dispatcher.SendAsync(new PlaceOrderCommand(request), cancellationToken);
        return result.Success ? Ok(result) : BadRequest(new ProblemDetails { Title = "order_failed", Detail = result.Message });
    }

    /// <summary>Mark an order as delivered (deducts wallet, consumes reserved stock).</summary>
    [HttpPost("{orderId:int}/deliver")]
    [ProducesResponseType(typeof(OrderStatusUpdateResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderStatusUpdateResultDto>> Deliver(int orderId, CancellationToken cancellationToken)
    {
        var result = await _dispatcher.SendAsync(new MarkOrderDeliveredCommand(orderId), cancellationToken);
        return Ok(result);
    }

    /// <summary>List pending orders for the operator queue.</summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(PagedOrdersResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedOrdersResultDto>> Pending([FromQuery] DateTime? date, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        => Ok(await _operatorQueryService.GetPendingOrdersAsync(date, page, pageSize));

    /// <summary>Free-text search across orders (number / user id).</summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(PagedOrdersResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedOrdersResultDto>> Search([FromQuery] string? q, [FromQuery] DateTime? date, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
        => Ok(await _operatorQueryService.SearchAllOrdersAsync(q ?? string.Empty, date, page, pageSize));
}
