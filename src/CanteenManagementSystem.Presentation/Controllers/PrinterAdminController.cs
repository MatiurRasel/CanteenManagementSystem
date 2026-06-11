// =============================================================================
// PrinterAdminController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Configure the per-tenant thermal-receipt printer (TCP / ESC-POS) and
// fire a test print to validate connectivity.
//
// SETTINGS PERSISTED
//   Printer.Network.Host        IP or hostname
//   Printer.Network.Port        default 9100
//   Printer.Network.TimeoutMs   default 4000
//   Printer.Network.LineWidth   32 for 58 mm paper, 48 for 80 mm
//
// PRINT-NOW
//   POST /admin/printer/test  -> calls INetworkReceiptPrinter.PrintTestAsync,
//   surfaces success/failure as a flash toast.
// =============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Audit;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Abstractions.Receipts;
using CanteenManagementSystem.Infrastructure.Receipts;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "Operator")]      // operators can fire prints + kick the drawer; tenant-admins can also configure
[Route("admin/printer")]
public sealed class PrinterAdminController : Controller
{
    private readonly ITenantSettings _settings;
    private readonly INetworkReceiptPrinter _printer;
    private readonly IAuditTrail _audit;

    public PrinterAdminController(ITenantSettings settings, INetworkReceiptPrinter printer, IAuditTrail audit)
    {
        _settings = settings;
        _printer = printer;
        _audit = audit;
    }

    public sealed class PrinterForm
    {
        public string? Host { get; set; }
        public int Port { get; set; } = 9100;
        public int TimeoutMs { get; set; } = 4000;
        public int LineWidth { get; set; } = 48;
    }

    [HttpGet("")]
    [Authorize(Policy = "TenantAdmin")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var form = new PrinterForm
        {
            Host      = await _settings.GetAsync(ReceiptPrinterSettingsKeys.Host, defaultValue: null, ct),
            Port      = await _settings.GetIntAsync(ReceiptPrinterSettingsKeys.Port, 9100, ct),
            TimeoutMs = await _settings.GetIntAsync(ReceiptPrinterSettingsKeys.TimeoutMs, 4000, ct),
            LineWidth = await _settings.GetIntAsync(ReceiptPrinterSettingsKeys.LineWidth, 48, ct)
        };
        return View(form);
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "TenantAdmin")]
    public async Task<IActionResult> Index(PrinterForm form, CancellationToken ct)
    {
        await _settings.SetAsync(ReceiptPrinterSettingsKeys.Host,      form.Host, cancellationToken: ct);
        await _settings.SetAsync(ReceiptPrinterSettingsKeys.Port,      form.Port.ToString(), cancellationToken: ct);
        await _settings.SetAsync(ReceiptPrinterSettingsKeys.TimeoutMs, form.TimeoutMs.ToString(), cancellationToken: ct);
        await _settings.SetAsync(ReceiptPrinterSettingsKeys.LineWidth, form.LineWidth.ToString(), cancellationToken: ct);
        TempData["Flash.Success"] = "Printer settings saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("test")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = "TenantAdmin")]
    public async Task<IActionResult> Test(CancellationToken ct)
    {
        var result = await _printer.PrintTestAsync(ct);
        if (result.Success) TempData["Flash.Success"] = $"Test print sent — {result.BytesSent} bytes.";
        else                TempData["Flash.Error"]   = "Test print failed: " + result.Message;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Open the cash drawer attached to the receipt printer. Every kick is audited
    /// with the calling user so it's traceable on the audit log viewer.
    /// </summary>
    [HttpPost("kick-drawer")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KickDrawer(string? reason, CancellationToken ct)
    {
        var result = await _printer.KickDrawerAsync(ct);
        await _audit.RecordAsync(
            action: "Drawer.Opened",
            entityType: "Printer",
            entityId: null,
            payload: new { reason = reason ?? "manual", success = result.Success, message = result.Message },
            cancellationToken: ct);

        if (Request.Headers.Accept.Any(h => h is not null && h.Contains("application/json")))
            return Json(new { success = result.Success, message = result.Message });

        if (result.Success) TempData["Flash.Success"] = "Cash drawer opened.";
        else                TempData["Flash.Error"]   = "Drawer kick failed: " + result.Message;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Re-print a previously placed receipt. Useful when the original print
    /// jammed or the customer asks for another copy. Records the re-print as
    /// an audit row so the operator can't quietly duplicate receipts.
    /// </summary>
    [HttpPost("reprint/{orderId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reprint(int orderId, CancellationToken ct)
    {
        var result = await _printer.PrintReceiptAsync(orderId, ct);
        await _audit.RecordAsync(
            action: "Receipt.Reprinted",
            entityType: "Order",
            entityId: orderId.ToString(),
            payload: new { success = result.Success, message = result.Message },
            cancellationToken: ct);

        if (Request.Headers.Accept.Any(h => h is not null && h.Contains("application/json")))
            return Json(new { success = result.Success, message = result.Message });

        if (result.Success) TempData["Flash.Success"] = $"Receipt #{orderId} re-printed.";
        else                TempData["Flash.Error"]   = "Re-print failed: " + result.Message;
        return Redirect(Request.Headers["Referer"].ToString() is { Length: > 0 } r ? r : Url.Action(nameof(Index))!);
    }
}
