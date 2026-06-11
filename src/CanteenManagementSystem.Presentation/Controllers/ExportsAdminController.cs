// =============================================================================
// ExportsAdminController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// /admin/exports/* — tenant-scoped CSV exports for finance / audit.
//
//   GET /admin/exports/orders.csv?from=...&to=...&status=...
//   GET /admin/exports/wallet.csv?from=...&to=...&userId=...
//
// CSV format: UTF-8 BOM + header row. Excel + Google Sheets open it correctly
// in any locale. Date range defaults to the last 7 days when caller omits.
// ADR 0004: controller depends on IAdminExportsService — no IAppDbContext.
// =============================================================================

using System.Globalization;
using System.Text;
using CanteenManagementSystem.Application.Exports;
using CanteenManagementSystem.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "TenantAdmin")]
[Route("admin/exports")]
public sealed class ExportsAdminController : Controller
{
    private const int MaxRows = 100_000;
    private readonly IAdminExportsService _exports;
    public ExportsAdminController(IAdminExportsService exports) => _exports = exports;

    [HttpGet("")]
    public IActionResult Index() => View();

    [HttpGet("orders.csv")]
    public async Task<IActionResult> Orders(
        DateTime? from, DateTime? to,
        CanteenOrderStatus? status,
        CancellationToken ct)
    {
        var (fromUtc, toUtc) = ResolveRange(from, to);
        var rows = await _exports.GetOrdersAsync(fromUtc, toUtc, status, MaxRows, ct);

        var sb = new StringBuilder();
        sb.Append('﻿');
        sb.AppendLine("OrderNumber,OrderDate,DeliveredDate,Status,UserId,UserType,TotalAmount,IsPreOrder,PickupAtUtc,TableNumber,Items");
        foreach (var r in rows)
        {
            sb.Append(CsvEscape(r.OrderNumber)).Append(',')
              .Append(r.OrderDate.ToString("o", CultureInfo.InvariantCulture)).Append(',')
              .Append(r.DeliveredDate?.ToString("o", CultureInfo.InvariantCulture) ?? "").Append(',')
              .Append(r.Status).Append(',')
              .Append(CsvEscape(r.UserId)).Append(',')
              .Append(r.UserType).Append(',')
              .Append(r.TotalAmount.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(r.IsPreOrder ? "true" : "false").Append(',')
              .Append(r.PickupAtUtc?.ToString("o", CultureInfo.InvariantCulture) ?? "").Append(',')
              .Append(CsvEscape(r.TableNumber)).Append(',')
              .Append(CsvEscape(r.Items))
              .AppendLine();
        }
        var name = $"orders_{fromUtc:yyyyMMdd}_{toUtc.AddDays(-1):yyyyMMdd}.csv";
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", name);
    }

    [HttpGet("wallet.csv")]
    public async Task<IActionResult> Wallet(
        DateTime? from, DateTime? to,
        string? userId,
        CancellationToken ct)
    {
        var (fromUtc, toUtc) = ResolveRange(from, to);
        var rows = await _exports.GetWalletLedgerAsync(fromUtc, toUtc, userId, MaxRows, ct);

        var sb = new StringBuilder();
        sb.Append('﻿');
        sb.AppendLine("LedgerId,UserId,EntryType,Amount,BalanceAfter,BlockedAfter,OrderId,Reason,CreatedAtUtc,CreatedBy,IdempotencyKey");
        foreach (var r in rows)
        {
            sb.Append(r.LedgerId).Append(',')
              .Append(CsvEscape(r.UserId)).Append(',')
              .Append(r.EntryType).Append(',')
              .Append(r.Amount.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(r.BalanceAfter.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(r.BlockedAfter.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(r.OrderId?.ToString() ?? "").Append(',')
              .Append(CsvEscape(r.Reason)).Append(',')
              .Append(r.CreatedAtUtc.ToString("o", CultureInfo.InvariantCulture)).Append(',')
              .Append(CsvEscape(r.CreatedBy)).Append(',')
              .Append(CsvEscape(r.IdempotencyKey))
              .AppendLine();
        }
        var name = $"wallet_{fromUtc:yyyyMMdd}_{toUtc.AddDays(-1):yyyyMMdd}.csv";
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", name);
    }

    private static (DateTime fromUtc, DateTime toUtc) ResolveRange(DateTime? from, DateTime? to)
    {
        var today = DateTime.UtcNow.Date;
        var f = (from ?? today.AddDays(-6)).Date;
        var t = (to ?? today).Date.AddDays(1);
        if (t <= f) t = f.AddDays(1);
        return (f, t);
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
