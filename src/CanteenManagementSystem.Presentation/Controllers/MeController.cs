// =============================================================================
// MeController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Per ADR 0004 the controller depends on IMeService — no IAppDbContext.
// =============================================================================

using System.Security.Claims;
using CanteenManagementSystem.Application.Me;
using CanteenManagementSystem.Domain.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "AuthenticatedAny")]
[Route("me")]
public sealed class MeController : Controller
{
    private readonly IMeService _me;
    private readonly IDataSubjectExportService _export;
    private readonly Platform.Application.Abstractions.Audit.IAuditTrail _audit;

    public MeController(
        IMeService me,
        IDataSubjectExportService export,
        Platform.Application.Abstractions.Audit.IAuditTrail audit)
    {
        _me = me;
        _export = export;
        _audit = audit;
    }

    public sealed record OrderHistoryVm(
        string? LinkedPersonId,
        IReadOnlyList<Order> Items,
        int Page, int PageSize, int TotalCount,
        decimal? CurrentBalance,
        decimal SpentThisMonth);

    [HttpGet("orders")]
    public async Task<IActionResult> Orders(int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        var uid = ResolveUserId();
        if (uid is null) return View(new OrderHistoryVm(null, Array.Empty<Order>(), 1, pageSize, 0, null, 0m));
        var p = await _me.GetOrderHistoryAsync(uid.Value, page, pageSize, ct);
        return View(new OrderHistoryVm(p.LinkedPersonId, p.Items, p.Page, p.PageSize, p.TotalCount, p.CurrentBalance, p.SpentThisMonth));
    }

    [HttpGet("orders.csv")]
    public async Task<IActionResult> ExportOrdersCsv(CancellationToken ct)
    {
        var uid = ResolveUserId();
        if (uid is null) return File(System.Text.Encoding.UTF8.GetBytes("(no orders)"), "text/csv", "orders.csv");
        var rows = await _me.ExportAllOrdersAsync(uid.Value, ct);
        var linkedId = rows.FirstOrDefault()?.UserId ?? "me";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("OrderNumber,OrderDate,Status,Items,TotalAmount");
        foreach (var o in rows)
        {
            var items = string.Join("; ", o.OrderItems.Select(i => $"{i.Quantity}× {i.FoodItem.ItemName}"));
            sb.Append(o.OrderNumber).Append(',')
              .Append(o.OrderDate.ToString("yyyy-MM-dd HH:mm")).Append(',')
              .Append(o.Status).Append(',')
              .Append(Escape(items)).Append(',')
              .Append(o.TotalAmount.ToString(System.Globalization.CultureInfo.InvariantCulture)).AppendLine();
        }
        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"orders_{linkedId}.csv");
    }

    /// <summary>
    /// GDPR Article 20 — Right to data portability. Returns a ZIP archive containing
    /// every row this user owns (profile, orders, wallet, ledger, notifications,
    /// favourites, card events). Audited on every download.
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> ExportData(CancellationToken ct)
    {
        var uid = ResolveUserId();
        if (uid is null) return RedirectToAction("Login", "Account");

        var archive = await _export.ExportAsync(uid.Value, ct);
        await _audit.RecordAsync(
            action: "Me.DataExported",
            entityType: "AppUser",
            entityId: uid.Value.ToString(),
            payload: new { archive.FileName, ByteLength = archive.Bytes.Length },
            cancellationToken: ct);
        return File(archive.Bytes, archive.ContentType, archive.FileName);
    }

    private int? ResolveUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var n) ? n : null;
    }

    private static string Escape(string v)
    {
        if (v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return v;
        return "\"" + v.Replace("\"", "\"\"") + "\"";
    }
}
