// =============================================================================
// NotificationDeliveriesController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Per-message history view. Lists rows from CanteenNotificationLog with filters
// (date, status, channel, template, recipient substring). Pagination + CSV
// export so support can grep for "did this user receive their order SMS?".
// =============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Application.Persistence;
using Platform.Domain.Notifications;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "TenantAdmin")]
[Route("admin/notifications/deliveries")]
public sealed class NotificationDeliveriesController : Controller
{
    private readonly IReadOnlyRepository<NotificationLog> _logs;

    public NotificationDeliveriesController(IReadOnlyRepository<NotificationLog> logs)
    {
        _logs = logs;
    }

    public sealed record DeliveryFilter(
        DateTime? From,
        DateTime? To,
        NotificationChannel? Channel,
        NotificationStatus? Status,
        string? TemplateKey,
        string? Recipient,
        int Page,
        int PageSize);

    public sealed record DeliveryPageVm(
        IReadOnlyList<NotificationLog> Rows,
        DeliveryFilter Filter,
        int TotalCount,
        int TotalPages);

    [HttpGet("")]
    public async Task<IActionResult> Index(
        DateTime? from, DateTime? to,
        NotificationChannel? channel, NotificationStatus? status,
        string? templateKey, string? recipient,
        int page = 1, int pageSize = 50,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 250);
        var query = BuildQuery(from, to, channel, status, templateKey, recipient);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(n => n.OccurredAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return View(new DeliveryPageVm(
            rows,
            new DeliveryFilter(from, to, channel, status, templateKey, recipient, page, pageSize),
            total,
            (int)Math.Ceiling(total / (double)pageSize)));
    }

    [HttpGet("export.csv")]
    public async Task<IActionResult> ExportCsv(
        DateTime? from, DateTime? to,
        NotificationChannel? channel, NotificationStatus? status,
        string? templateKey, string? recipient,
        CancellationToken ct = default)
    {
        var query = BuildQuery(from, to, channel, status, templateKey, recipient);
        var rows = await query.OrderByDescending(n => n.OccurredAtUtc).Take(50_000).ToListAsync(ct);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("OccurredAtUtc,DeliveredAtUtc,TemplateKey,Channel,Status,Recipient,Subject,ProviderId,AttemptCount,FailureReason");
        foreach (var r in rows)
        {
            sb.Append(r.OccurredAtUtc.ToString("o")).Append(',')
              .Append(r.DeliveredAtUtc?.ToString("o")).Append(',')
              .Append(C(r.TemplateKey)).Append(',')
              .Append(r.Channel).Append(',')
              .Append(r.Status).Append(',')
              .Append(C(r.Recipient)).Append(',')
              .Append(C(r.Subject)).Append(',')
              .Append(C(r.ProviderId)).Append(',')
              .Append(r.AttemptCount).Append(',')
              .Append(C(r.FailureReason)).AppendLine();
        }
        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv",
            $"notification-deliveries-{DateTime.UtcNow:yyyyMMddHHmm}.csv");
    }

    private IQueryable<NotificationLog> BuildQuery(
        DateTime? from, DateTime? to,
        NotificationChannel? channel, NotificationStatus? status,
        string? templateKey, string? recipient)
    {
        var q = _logs.NoTrackingQuery();
        if (from.HasValue) q = q.Where(n => n.OccurredAtUtc >= from.Value);
        if (to.HasValue)   q = q.Where(n => n.OccurredAtUtc < to.Value.AddDays(1));
        if (channel.HasValue) q = q.Where(n => n.Channel == channel.Value);
        if (status.HasValue)  q = q.Where(n => n.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(templateKey))
            q = q.Where(n => n.TemplateKey == templateKey);
        if (!string.IsNullOrWhiteSpace(recipient))
        {
            var needle = recipient.Trim();
            q = q.Where(n => n.Recipient != null && n.Recipient.Contains(needle));
        }
        return q;
    }

    private static string C(string? v)
    {
        if (string.IsNullOrEmpty(v)) return string.Empty;
        if (v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return v;
        return "\"" + v.Replace("\"", "\"\"") + "\"";
    }
}
