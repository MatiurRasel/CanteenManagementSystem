using CanteenManagementSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Controllers.Api
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class NfcController : ControllerBase
    {
        private readonly INfcService _nfcService;
        private readonly ILogger<NfcController> _logger;

        public NfcController(INfcService nfcService, ILogger<NfcController> logger)
        {
            _nfcService = nfcService;
            _logger = logger;
        }

        [HttpPost("authenticate")]
        public async Task<ActionResult<NfcUserInfo>> Authenticate([FromBody] NfcAuthRequest request)
        {
            try
            {
                var userInfo = await _nfcService.AuthenticateCardAsync(request.CardNumber);
                if (userInfo == null)
                    return NotFound(new { status = "error", message = "Card not found or inactive" });

                return Ok(new { status = "success", data = userInfo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error authenticating NFC card");
                return StatusCode(500, new { status = "error", message = "Internal server error" });
            }
        }

        [HttpPost("assign")]
        public async Task<ActionResult> AssignCard([FromBody] AssignCardRequest request)
        {
            try
            {
                var result = await _nfcService.AssignCardAsync(request.CardNumber, request.UserId);
                if (result)
                    return Ok(new { status = "success", message = "Card assigned successfully" });
                return BadRequest(new { status = "error", message = "Failed to assign card" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning card");
                return StatusCode(500, new { status = "error", message = "Internal server error" });
            }
        }

        [HttpPost("block")]
        public async Task<ActionResult> BlockCard([FromBody] BlockCardRequest request)
        {
            try
            {
                var result = await _nfcService.BlockCardAsync(request.CardNumber);
                if (result)
                    return Ok(new { status = "success", message = "Card blocked successfully" });
                return BadRequest(new { status = "error", message = "Failed to block card" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error blocking card");
                return StatusCode(500, new { status = "error", message = "Internal server error" });
            }
        }
    }

    public class NfcAuthRequest
    {
        public string CardNumber { get; set; } = string.Empty;
    }

    public class AssignCardRequest
    {
        public string CardNumber { get; set; } = string.Empty;
        public Guid UserId { get; set; }
    }

    public class BlockCardRequest
    {
        public string CardNumber { get; set; } = string.Empty;
    }
}

