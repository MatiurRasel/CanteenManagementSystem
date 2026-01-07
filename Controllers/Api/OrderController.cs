using CanteenManagementSystem.Application.DTOs;
using CanteenManagementSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Controllers.Api
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<OrderController> _logger;

        public OrderController(IOrderService orderService, ILogger<OrderController> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<OrderDto>> PlaceOrder([FromBody] PlaceOrderRequest request, [FromQuery] Guid? userId)
        {
            try
            {
                var order = await _orderService.PlaceOrderAsync(request, userId);
                return Ok(new { status = "success", data = order });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { status = "error", message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error placing order");
                return StatusCode(500, new { status = "error", message = "Internal server error" });
            }
        }

        [HttpGet("{orderId}")]
        public async Task<ActionResult<OrderDto>> GetOrder(Guid orderId)
        {
            try
            {
                var order = await _orderService.GetOrderAsync(orderId);
                return Ok(new { status = "success", data = order });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { status = "error", message = ex.Message });
            }
        }

        [HttpGet("user/{userId}")]
        public async Task<ActionResult<List<OrderDto>>> GetUserOrders(Guid userId)
        {
            try
            {
                var orders = await _orderService.GetUserOrdersAsync(userId);
                return Ok(new { status = "success", data = orders });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user orders");
                return StatusCode(500, new { status = "error", message = "Internal server error" });
            }
        }

        [HttpDelete("{orderId}")]
        public async Task<ActionResult> CancelOrder(Guid orderId, [FromQuery] Guid userId)
        {
            try
            {
                var result = await _orderService.CancelOrderAsync(orderId, userId);
                if (result)
                    return Ok(new { status = "success", message = "Order cancelled" });
                return BadRequest(new { status = "error", message = "Cannot cancel order" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling order");
                return StatusCode(500, new { status = "error", message = "Internal server error" });
            }
        }

        [HttpPost("{orderId}/deliver")]
        public async Task<ActionResult> ConfirmDelivery(Guid orderId, [FromBody] DeliveryRequest request)
        {
            try
            {
                var result = await _orderService.ConfirmDeliveryAsync(orderId, request.NfcCardNumber);
                if (result)
                    return Ok(new { status = "success", message = "Order delivered" });
                return BadRequest(new { status = "error", message = "Cannot deliver order" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming delivery");
                return StatusCode(500, new { status = "error", message = "Internal server error" });
            }
        }

        [HttpPost("{orderId}/ready")]
        public async Task<ActionResult> MarkReady(Guid orderId, [FromBody] ReadyRequest request)
        {
            try
            {
                var result = await _orderService.MarkOrderReadyAsync(orderId, request.OperatorId);
                if (result)
                    return Ok(new { status = "success", message = "Order marked as ready" });
                return BadRequest(new { status = "error", message = "Cannot mark order as ready" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking order ready");
                return StatusCode(500, new { status = "error", message = "Internal server error" });
            }
        }

        [HttpGet("pending/{clientId}")]
        public async Task<ActionResult<List<OrderDto>>> GetPendingOrders(Guid clientId)
        {
            try
            {
                var orders = await _orderService.GetPendingOrdersAsync(clientId);
                return Ok(new { status = "success", data = orders });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending orders");
                return StatusCode(500, new { status = "error", message = "Internal server error" });
            }
        }
    }

    public class DeliveryRequest
    {
        public string NfcCardNumber { get; set; } = string.Empty;
    }

    public class ReadyRequest
    {
        public Guid OperatorId { get; set; }
    }
}

