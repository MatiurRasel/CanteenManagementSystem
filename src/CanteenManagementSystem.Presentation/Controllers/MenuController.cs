// =============================================================================
// MenuController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Per ADR 0004 the controller depends only on IMenuManagementService.
// =============================================================================

using CanteenManagementSystem.Application.Menus;
using CanteenManagementSystem.Application.Menus.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "Operator")]
public class MenuController : Controller
{
    private readonly IMenuManagementService _menu;
    public MenuController(IMenuManagementService menu) => _menu = menu;

    // PRG (POST→Redirect→GET) so the date filter never shows in the URL.
    // GET reads the date from session (set on the prior POST); falls back to today.
    // The POST handler uses a distinct C# name + [ActionName] so MVC's action
    // selector never mis-routes a legacy GET ?date=… to the POST overload (which
    // would 405). The GET accepts an optional `date` query string for back-compat:
    // it stashes the value in session and redirects to a clean URL.
    private const string SessionKey_MenuManageDate = "Menu.Manage.Date";

    [HttpGet]
    public async Task<IActionResult> Manage(DateTime? date)
    {
        if (date.HasValue)
        {
            HttpContext.Session.SetString(SessionKey_MenuManageDate, date.Value.ToString("yyyy-MM-dd"));
            return RedirectToAction(nameof(Manage));   // → clean URL
        }
        var raw = HttpContext.Session.GetString(SessionKey_MenuManageDate);
        DateTime? saved = DateTime.TryParse(raw, out var d) ? d : null;
        return View(await _menu.GetManageViewModelAsync(saved));
    }

    [HttpPost]
    [ActionName(nameof(Manage))]
    [ValidateAntiForgeryToken]
    public IActionResult ManagePost(DateTime date)
    {
        HttpContext.Session.SetString(SessionKey_MenuManageDate, date.ToString("yyyy-MM-dd"));
        return RedirectToAction(nameof(Manage));
    }

    [HttpPost] public Task<IActionResult> AddMenuItem([FromBody] AddMenuItemRequestDto request)
        => JsonResult(_menu.CreateMenuItemAsync(request));

    [HttpPost] public Task<IActionResult> RemoveMenuItem(int menuId)
        => JsonResult(_menu.RemoveMenuItemAsync(menuId));

    [HttpPost] public Task<IActionResult> UpdateQuantity([FromBody] UpdateQuantityRequestDto request)
        => JsonResult(_menu.UpdateQuantityAsync(request));

    [HttpPost] public Task<IActionResult> ToggleAvailability(int menuId)
        => JsonResult(_menu.ToggleAvailabilityAsync(menuId));

    [HttpPost] public Task<IActionResult> RemoveFromMenu(int menuId)
        => JsonResult(_menu.RemoveFromMenuAsync(menuId));

    public async Task<IActionResult> ManageItems()
        => View(await _menu.GetManageItemsAsync());

    [HttpPost] public Task<IActionResult> CreateItem([FromBody] CreateFoodItemRequestDto request)
        => JsonResult(_menu.CreateItemAsync(request));

    [HttpPost] public Task<IActionResult> UpdateItem([FromBody] UpdateFoodItemRequestDto request)
        => JsonResult(_menu.UpdateItemAsync(request));

    [HttpPost] public Task<IActionResult> CreateWeeklyTemplate([FromBody] CreateTemplateRequestDto request)
        => JsonResult(_menu.CreateWeeklyTemplateAsync(request));

    [HttpPost] public Task<IActionResult> DeleteWeeklyTemplate(int templateId)
        => JsonResult(_menu.DeleteWeeklyTemplateAsync(templateId));

    [HttpPost] public Task<IActionResult> ApplyWeeklyTemplate([FromBody] ApplyWeeklyTemplateRequestDto request)
        => JsonResult(_menu.ApplyWeeklyTemplateAsync(request));

    [HttpPost] public Task<IActionResult> CopyMenu([FromBody] CopyMenuRequestDto request)
        => JsonResult(_menu.CopyMenuAsync(request));

    [HttpGet]
    public async Task<IActionResult> GetWeeklyTemplate(int dayOfWeek, CancellationToken ct)
    {
        var templates = await _menu.GetWeeklyTemplateAsync(dayOfWeek, ct);
        return Json(new { success = true, templates });
    }

    [HttpGet]
    public async Task<IActionResult> GetFoodItem(int id, CancellationToken ct)
    {
        var item = await _menu.GetFoodItemAsync(id, ct);
        if (item is null) return Json(new { success = false, message = "খাবার আইটেম খুঁজে পাওয়া যায়নি" });
        return Json(new { success = true, item });
    }

    [HttpPost] public Task<IActionResult> CreateFoodItem([FromBody] CreateFoodItemRequestDto request)
        => JsonResult(_menu.CreateItemAsync(request));

    [HttpPost] public Task<IActionResult> UpdateFoodItem([FromBody] UpdateFoodItemRequestDto request)
        => JsonResult(_menu.UpdateItemAsync(request));

    [HttpPost] public Task<IActionResult> ToggleFoodItemStatus(int id)
        => JsonResult(_menu.ToggleFoodItemStatusAsync(id));

    [HttpGet]
    public async Task<IActionResult> GetMenuPreview(DateTime date)
        => Json(new { success = true, items = await _menu.GetMenuPreviewAsync(date) });

    [HttpPost] public Task<IActionResult> QuickApplyTemplate([FromBody] QuickApplyTemplateRequestDto request)
        => JsonResult(_menu.QuickApplyTemplateAsync(request));

    [HttpPost] public Task<IActionResult> ApplyDayTemplate(int dayOfWeek)
        => JsonResult(_menu.ApplyDayTemplateAsync(dayOfWeek));

    [HttpPost] public Task<IActionResult> BatchUpdateQuantities([FromBody] BatchUpdateRequestDto request)
        => JsonResult(_menu.BatchUpdateQuantitiesAsync(request));

    [HttpPost] public Task<IActionResult> BatchToggleAvailability([FromBody] BatchToggleRequestDto request)
        => JsonResult(_menu.BatchToggleAvailabilityAsync(request));

    [HttpPost] public Task<IActionResult> ClearTodayMenu()
        => JsonResult(_menu.ClearTodayMenuAsync());

    [HttpPost] public Task<IActionResult> CreateBulkTemplate([FromBody] BulkTemplateRequestDto request)
        => JsonResult(_menu.CreateBulkTemplateAsync(request));

    [HttpGet]
    public async Task<IActionResult> ExportTemplates()
        => Json(new { success = true, templates = await _menu.ExportTemplatesAsync() });

    [HttpGet]
    public async Task<IActionResult> GenerateWeeklyReport()
        => Json(new { success = true, report = await _menu.GenerateWeeklyReportAsync() });

    private async Task<IActionResult> JsonResult<T>(Task<T> work) => Json(await work);
}
