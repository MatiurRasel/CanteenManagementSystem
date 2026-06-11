using Asp.Versioning;
using CanteenManagementSystem.Application.Menus;
using CanteenManagementSystem.Application.Menus.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Presentation.Controllers.Api.V1;

/// Menu management endpoints (admin).
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/menu")]
[Produces("application/json")]
public sealed class MenuController : ControllerBase
{
    private readonly IMenuManagementService _menus;

    public MenuController(IMenuManagementService menus) => _menus = menus;

    /// <summary>Today's (or any date's) menu preview.</summary>
    [HttpGet("preview")]
    public async Task<ActionResult<IReadOnlyCollection<MenuPreviewItemDto>>> Preview([FromQuery] DateTime? date)
        => Ok(await _menus.GetMenuPreviewAsync(date ?? DateTime.Today));

    /// <summary>Add a single item to the daily menu.</summary>
    [HttpPost]
    public async Task<ActionResult<MenuOperationResultDto>> Add([FromBody] AddMenuItemRequestDto request)
        => Ok(await _menus.CreateMenuItemAsync(request));

    /// <summary>Update an item's available quantity (cannot drop below already-ordered).</summary>
    [HttpPatch("quantity")]
    public async Task<ActionResult<MenuOperationResultDto>> UpdateQuantity([FromBody] UpdateQuantityRequestDto request)
        => Ok(await _menus.UpdateQuantityAsync(request));

    /// <summary>Toggle availability on a menu line.</summary>
    [HttpPost("{menuId:int}/toggle")]
    public async Task<ActionResult<MenuOperationResultDto>> Toggle(int menuId)
        => Ok(await _menus.ToggleAvailabilityAsync(menuId));

    /// <summary>Apply a weekly template to a date range.</summary>
    [HttpPost("templates/apply")]
    public async Task<ActionResult<MenuOperationResultDto>> ApplyTemplate([FromBody] ApplyWeeklyTemplateRequestDto request)
        => Ok(await _menus.ApplyWeeklyTemplateAsync(request));

    /// <summary>Copy a day's menu to another day (with optional overwrite).</summary>
    [HttpPost("copy")]
    public async Task<ActionResult<MenuOperationResultDto>> Copy([FromBody] CopyMenuRequestDto request)
        => Ok(await _menus.CopyMenuAsync(request));
}
