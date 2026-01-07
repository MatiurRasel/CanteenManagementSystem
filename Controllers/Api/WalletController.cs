using CanteenManagementSystem.Application.DTOs;
using CanteenManagementSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Controllers.Api
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class WalletController : ControllerBase
    {
        private readonly IWalletService _walletService;
        private readonly ILogger<WalletController> _logger;

        public WalletController(IWalletService walletService, ILogger<WalletController> logger)
        {
            _walletService = walletService;
            _logger = logger;
        }

        [HttpGet("{userId}")]
        public async Task<ActionResult<WalletDto>> GetWallet(Guid userId)
        {
            try
            {
                var wallet = await _walletService.GetWalletAsync(userId);
                return Ok(new { status = "success", data = wallet });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { status = "error", message = ex.Message });
            }
        }

        [HttpGet("{userId}/transactions")]
        public async Task<ActionResult<List<TransactionDto>>> GetTransactions(
            Guid userId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate)
        {
            try
            {
                var transactions = await _walletService.GetTransactionsAsync(userId, fromDate, toDate);
                return Ok(new { status = "success", data = transactions });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transactions");
                return StatusCode(500, new { status = "error", message = "Internal server error" });
            }
        }

        [HttpPost("{userId}/recharge")]
        public async Task<ActionResult<RechargeResponse>> InitiateRecharge(Guid userId, [FromBody] RechargeRequest request)
        {
            try
            {
                var response = await _walletService.InitiateRechargeAsync(userId, request);
                return Ok(new { status = "success", data = response });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { status = "error", message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating recharge");
                return StatusCode(500, new { status = "error", message = "Internal server error" });
            }
        }

        [HttpPost("recharge/verify")]
        public async Task<ActionResult> VerifyRecharge([FromBody] RechargeVerifyRequest request)
        {
            try
            {
                var result = await _walletService.ProcessRechargeAsync(request.OrderReference, request.PaymentGatewayTxnId);
                if (result)
                    return Ok(new { status = "success", message = "Recharge processed successfully" });
                return BadRequest(new { status = "error", message = "Failed to process recharge" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying recharge");
                return StatusCode(500, new { status = "error", message = "Internal server error" });
            }
        }
    }

    public class RechargeVerifyRequest
    {
        public string OrderReference { get; set; } = string.Empty;
        public string PaymentGatewayTxnId { get; set; } = string.Empty;
    }
}

