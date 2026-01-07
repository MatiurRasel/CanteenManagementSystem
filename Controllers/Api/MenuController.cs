using CanteenManagementSystem.Application.DTOs;
using CanteenManagementSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Controllers.Api
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class MenuController : ControllerBase
    {
        private readonly IMenuService _menuService;
        private readonly ILogger<MenuController> _logger;

        public MenuController(IMenuService menuService, ILogger<MenuController> logger)
        {
            _menuService = menuService;
            _logger = logger;
        }

        [HttpGet("{clientId}")]
        public async Task<ActionResult<List<MenuCategoryDto>>> GetMenu(Guid clientId, [FromQuery] string? role)
        {
            try
            {
                var menu = await _menuService.GetMenuAsync(clientId, role);
                return Ok(new { status = "success", data = menu });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting menu");
                return StatusCode(500, new { status = "error", message = "Internal server error" });
            }
        }

        [HttpGet("items/{itemId}")]
        public async Task<ActionResult<MenuItemDto>> GetMenuItem(Guid itemId)
        {
            try
            {
                var item = await _menuService.GetMenuItemAsync(itemId);
                return Ok(new { status = "success", data = item });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { status = "error", message = ex.Message });
            }
        }

        [HttpPatch("items/{itemId}/availability")]
        public async Task<ActionResult> UpdateAvailability(Guid itemId, [FromBody] AvailabilityRequest request)
        {
            try
            {
                var result = await _menuService.UpdateItemAvailabilityAsync(itemId, request.IsAvailable);
                if (result)
                    return Ok(new { status = "success", message = "Availability updated" });
                return BadRequest(new { status = "error", message = "Failed to update availability" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating availability");
                return StatusCode(500, new { status = "error", message = "Internal server error" });
            }
        }

        [HttpGet("{clientId}/featured")]
        public async Task<ActionResult<List<MenuItemDto>>> GetFeaturedItems(Guid clientId)
        {
            try
            {
                var items = await _menuService.GetFeaturedItemsAsync(clientId);
                return Ok(new { status = "success", data = items });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting featured items");
                return StatusCode(500, new { status = "error", message = "Internal server error" });
            }
        }
    }

    public class AvailabilityRequest
    {
        public bool IsAvailable { get; set; }
    }
}

