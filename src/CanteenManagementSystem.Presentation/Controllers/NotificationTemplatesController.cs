// =============================================================================
// NotificationTemplatesController  (CanteenManagementSystem.Presentation.Controllers)
// -----------------------------------------------------------------------------
// Tenant-admin UI for editing notification templates. Templates live in
// CanteenTenantSettings under canonical keys:
//
//   Notifications.Template.{key}.subject
//   Notifications.Template.{key}.body
//
// where {key} matches one of `Platform.Application.Abstractions.Notifications.NotificationTemplates`.
//
// TOKENS available inside subject/body strings (resolved by INotificationService
// when sending): `{customer.name}`, `{order.number}`, `{order.total}`,
// `{wallet.balance}`, etc. — exact set depends on the trigger context.
// =============================================================================

using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Abstractions.Notifications;
using Platform.Domain.Notifications;

namespace CanteenManagementSystem.Presentation.Controllers;

[Authorize(Policy = "TenantAdmin")]
[Route("admin/notification-templates")]
public sealed class NotificationTemplatesController : Controller
{
    private readonly ITenantSettings _settings;
    private readonly INotificationService _notifications;

    public NotificationTemplatesController(ITenantSettings settings, INotificationService notifications)
    {
        _settings = settings;
        _notifications = notifications;
    }

    /// <summary>One row per known template key, with current subject + body resolved from tenant settings.</summary>
    public sealed record TemplateRowVm(string Key, string FriendlyName, string? Subject, string? Body);

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var keys = DiscoverTemplateKeys();
        var rows = new List<TemplateRowVm>(keys.Count);
        foreach (var key in keys)
        {
            var subject = await _settings.GetAsync(SubjectKey(key), defaultValue: null, ct);
            var body    = await _settings.GetAsync(BodyKey(key),    defaultValue: null, ct);
            rows.Add(new TemplateRowVm(key, Humanise(key), subject, body));
        }
        return View(rows.OrderBy(r => r.Key).ToList());
    }

    public sealed class EditForm
    {
        public string Key { get; set; } = string.Empty;
        public string? Subject { get; set; }
        public string? Body { get; set; }
    }

    [HttpGet("{key}")]
    public async Task<IActionResult> Edit(string key, CancellationToken ct)
    {
        if (!DiscoverTemplateKeys().Contains(key)) return NotFound();
        var form = new EditForm
        {
            Key     = key,
            Subject = await _settings.GetAsync(SubjectKey(key), defaultValue: null, ct),
            Body    = await _settings.GetAsync(BodyKey(key),    defaultValue: null, ct)
        };
        ViewBag.FriendlyName = Humanise(key);
        return View(form);
    }

    [HttpPost("{key}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string key, EditForm form, CancellationToken ct)
    {
        if (!DiscoverTemplateKeys().Contains(key)) return NotFound();
        await _settings.SetAsync(SubjectKey(key), form.Subject, cancellationToken: ct);
        await _settings.SetAsync(BodyKey(key),    form.Body,    cancellationToken: ct);
        TempData["Flash.Success"] = $"Template '{key}' saved.";
        return RedirectToAction(nameof(Index));
    }

    public sealed class TestSendForm
    {
        public string Key { get; set; } = string.Empty;
        public string Channel { get; set; } = "Sms";       // "Sms" | "Email"
        public string Recipient { get; set; } = string.Empty;
    }

    /// Send the CURRENTLY-SAVED template to a chosen recipient as a smoke test.
    /// Token placeholders are filled with sample data so the admin can preview
    /// the rendered message end-to-end through the actual gateway.
    [HttpPost("{key}/test-send")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestSend(string key, TestSendForm form, CancellationToken ct)
    {
        if (!DiscoverTemplateKeys().Contains(key)) return NotFound();
        if (string.IsNullOrWhiteSpace(form.Recipient))
        {
            TempData["Flash.Error"] = "Recipient required (phone for SMS, email for Email).";
            return RedirectToAction(nameof(Edit), new { key });
        }

        var subject = await _settings.GetAsync(SubjectKey(key), defaultValue: $"[Canteen Test] {key}", ct);
        var body    = await _settings.GetAsync(BodyKey(key),    defaultValue: $"Hello {{customer.name}} — this is a test send of {key} template.", ct);
        var rendered = FillTokens(body ?? string.Empty);
        if (!Enum.TryParse<NotificationChannel>(form.Channel, true, out var channel)) channel = NotificationChannel.Sms;

        try
        {
            await _notifications.SendRawAsync(channel, form.Recipient.Trim(), subject, rendered, ct);
            TempData["Flash.Success"] = $"Test {channel} sent to {form.Recipient}.";
        }
        catch (Exception ex)
        {
            TempData["Flash.Error"] = $"Test send failed: {ex.Message}";
        }
        return RedirectToAction(nameof(Edit), new { key });
    }

    /// Replace every `{path.token}` with a sample value so the admin sees a fully-rendered message.
    private static string FillTokens(string body) => body
        .Replace("{customer.name}",     "Imran Ahmed")
        .Replace("{customer.phone}",    "01700000001")
        .Replace("{order.number}",      "ORD-260601-001")
        .Replace("{order.total}",       "120")
        .Replace("{order.status}",      "Ready")
        .Replace("{wallet.balance}",    "850")
        .Replace("{wallet.amount}",     "500")
        .Replace("{tenant.name}",       "Canteen");

    // ─── helpers ──────────────────────────────────────────────────────────

    private static HashSet<string> DiscoverTemplateKeys()
    {
        // Reflect over NotificationTemplates static constants so adding a new
        // template name on the platform side automatically surfaces it here.
        return typeof(NotificationTemplates)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string SubjectKey(string key) => $"Notifications.Template.{key}.subject";
    private static string BodyKey(string key)    => $"Notifications.Template.{key}.body";

    private static string Humanise(string key)
    {
        // "order.ready" -> "Order Ready"
        return string.Join(' ', key.Split('.', '_')
            .Select(seg => seg.Length == 0 ? seg : char.ToUpperInvariant(seg[0]) + seg[1..].ToLowerInvariant()));
    }
}
