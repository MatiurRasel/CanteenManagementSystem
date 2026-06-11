// =============================================================================
// INotificationService  (Application)
// -----------------------------------------------------------------------------
// Single entry-point for sending tenant-branded messages. The service:
//   1. Renders a template (tokens like {customer.name}, {order.number}).
//   2. Resolves the recipient address (phone for SMS, email for SMTP, etc.).
//   3. Enqueues a NotificationLog row in status=Queued.
//   4. Returns immediately. The dispatcher hosted service picks the row up
//      and calls the appropriate INotificationChannel impl.
//
// USAGE EXAMPLES
//   await notifications.SendAsync(NotificationTemplates.OrderReady, "01700000000",
//     new { customer = "Anik", order = new { number = "ORD-241101-0042" } });
//
//   await notifications.SendAsync(NotificationTemplates.RechargeOk, "anik@school.edu",
//     new { amount = 500, balance = 1500 });
//
// CONTROL
//   Templates live in DB (TenantSetting key "Notifications.Template.{key}.body")
//   so wording can be tweaked per tenant without code changes.
// =============================================================================

using Platform.Domain.Notifications;

namespace Platform.Application.Abstractions.Notifications;

public interface INotificationService
{
    /// <summary>
    /// Enqueue a notification using a named template + tokens.
    ///
    /// <para>When <paramref name="userId"/> is supplied, the service first
    /// checks <see cref="Platform.Domain.Notifications.UserNotificationPreference"/>
    /// for a matching opt-out and silently drops the message (returning 0)
    /// if the user has disabled this template / channel. Pass null when
    /// dispatching to non-user recipients (operations alerts, system warnings).</para>
    /// </summary>
    /// <returns>The created NotificationLog id, or 0 if suppressed by opt-out.</returns>
    Task<long> SendAsync(
        string templateKey,
        string recipient,
        object tokens,
        NotificationChannel? overrideChannel = null,
        string? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Send a free-text message immediately (no template). Useful for ad-hoc tests.</summary>
    Task<long> SendRawAsync(
        NotificationChannel channel,
        string recipient,
        string? subject,
        string body,
        CancellationToken cancellationToken = default);
}

/// <summary>Named template constants. Each must have rows under TenantSetting:
/// Notifications.Template.{key}.subject and .body.</summary>
public static class NotificationTemplates
{
    public const string OrderPlaced       = "order.placed";
    public const string OrderReady        = "order.ready";
    public const string OrderDelivered    = "order.delivered";
    public const string OrderCancelled    = "order.cancelled";
    public const string LowBalance        = "wallet.low_balance";
    public const string RechargeOk        = "wallet.recharge_ok";
    public const string EmergencyUsed     = "wallet.emergency_used";
    public const string ParentChildOrder  = "parent.child_order";
    public const string ParentWeeklySpend = "parent.weekly_spend";
    public const string AllergyWarning    = "counter.allergy_warning";
    public const string LowStockAlert     = "inventory.low_stock";
}
