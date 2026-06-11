// =============================================================================
// NotificationService  (Infrastructure)
// -----------------------------------------------------------------------------
// Implements INotificationService. Resolves template -> renders body -> writes
// NotificationLog (status=Queued). The hosted dispatcher (NotificationDispatcher)
// picks it up and routes to the right INotificationChannel.
//
// TEMPLATE RESOLUTION ORDER
//   1. Tenant DB row: Notifications.Template.{key}.body / .subject / .channel
//   2. appsettings.json fallback (same keys)
//   3. Built-in default messages (see TemplateDefaults dictionary below)
//
// ADR 0004: IUnitOfWork + IRepository<NotificationLog>.
// =============================================================================

using Platform.Application.Persistence;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Abstractions.Notifications;
using Platform.Application.Abstractions.Time;
using Platform.Domain.Notifications;
using Microsoft.Extensions.Logging;

namespace Platform.Infrastructure.Notifications;

public sealed class NotificationService : INotificationService
{
    // Sensible defaults so the system can send notifications even before
    // an admin tweaks templates. Override in DB to customise per tenant.
    private static readonly Dictionary<string, (string Subject, string Body, NotificationChannel Channel)> TemplateDefaults = new()
    {
        [NotificationTemplates.OrderPlaced]      = ("Order placed",     "Your order {order.number} for {order.total} BDT is placed.", NotificationChannel.Sms),
        [NotificationTemplates.OrderReady]       = ("Order ready",      "Order {order.number} is ready to collect.",                  NotificationChannel.Sms),
        [NotificationTemplates.OrderDelivered]   = ("Order delivered",  "Order {order.number} delivered. Balance: {balance} BDT.",    NotificationChannel.Sms),
        [NotificationTemplates.OrderCancelled]   = ("Order cancelled",  "Order {order.number} was cancelled. Reason: {reason}.",      NotificationChannel.Sms),
        [NotificationTemplates.LowBalance]       = ("Low balance",      "Balance is low for {user}. Available: {balance} BDT.",       NotificationChannel.Sms),
        [NotificationTemplates.RechargeOk]       = ("Recharge ok",      "Recharge of {amount} BDT successful. New balance: {balance} BDT.", NotificationChannel.Sms),
        [NotificationTemplates.EmergencyUsed]    = ("Emergency used",   "Emergency balance used for {user}. Top up to recover.",      NotificationChannel.Sms),
        [NotificationTemplates.ParentChildOrder] = ("Child order",      "{child} ordered {order.items} for {order.total} BDT.",        NotificationChannel.Sms),
        [NotificationTemplates.ParentWeeklySpend]= ("Weekly spend",     "{child} spent {amount} BDT this week across {orders} orders. Top items: {topItems}.", NotificationChannel.Email),
        [NotificationTemplates.AllergyWarning]   = ("Allergy warning",  "{user} has an allergy to {allergen}. Operator confirmed before placing {item}.", NotificationChannel.Sms),
        [NotificationTemplates.LowStockAlert]    = ("Low stock",        "{item} has {qty} left (threshold {threshold}). Restock before the rush.", NotificationChannel.Email),
    };

    private readonly IUnitOfWork _uow;
    private readonly IReadOnlyRepository<UserNotificationPreference> _prefs;
    private readonly ITenantSettings _settings;
    private readonly IClock _clock;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IUnitOfWork uow,
        IReadOnlyRepository<UserNotificationPreference> prefs,
        ITenantSettings settings,
        IClock clock,
        ILogger<NotificationService> logger)
    {
        _uow = uow; _prefs = prefs; _settings = settings; _clock = clock; _logger = logger;
    }

    private IRepository<NotificationLog> Logs => _uow.Repository<NotificationLog>();

    public async Task<long> SendAsync(
        string templateKey,
        string recipient,
        object tokens,
        NotificationChannel? overrideChannel = null,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (!TemplateDefaults.TryGetValue(templateKey, out var defaults))
        {
            defaults = ("", "{message}", NotificationChannel.Sms);
        }

        var subjectTemplate = await _settings.GetAsync($"Notifications.Template.{templateKey}.subject", defaults.Subject, cancellationToken) ?? defaults.Subject;
        var bodyTemplate    = await _settings.GetAsync($"Notifications.Template.{templateKey}.body",    defaults.Body,    cancellationToken) ?? defaults.Body;
        var channelText     = await _settings.GetAsync($"Notifications.Template.{templateKey}.channel", defaults.Channel.ToString(), cancellationToken);
        var channel = overrideChannel
            ?? (Enum.TryParse<NotificationChannel>(channelText, ignoreCase: true, out var c) ? c : defaults.Channel);

        // Respect user opt-out preferences. A preference row matching
        //   (UserId, TemplateKey == templateKey || "*", Channel == channel || null)
        // means the user has opted out — silently drop the message.
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var optedOut = await _prefs.AnyAsync(
                p => p.UserId == userId
                  && (p.TemplateKey == templateKey || p.TemplateKey == "*")
                  && (p.Channel == channel || p.Channel == null),
                cancellationToken);
            if (optedOut)
            {
                _logger.LogDebug("Notification suppressed by user opt-out: user={User} template={Template} channel={Channel}",
                    userId, templateKey, channel);
                return 0;
            }
        }

        var log = new NotificationLog
        {
            TemplateKey = templateKey,
            Channel = channel,
            Status = NotificationStatus.Queued,
            Recipient = recipient,
            Subject = TemplateRenderer.Render(subjectTemplate, tokens),
            Body = TemplateRenderer.Render(bodyTemplate, tokens),
            OccurredAtUtc = _clock.UtcNow
        };

        await Logs.AddAsync(log, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Queued notification {Template} for {Recipient} via {Channel}", templateKey, recipient, channel);
        return log.NotificationId;
    }

    public async Task<long> SendRawAsync(NotificationChannel channel, string recipient, string? subject, string body, CancellationToken cancellationToken = default)
    {
        var log = new NotificationLog
        {
            TemplateKey = "raw",
            Channel = channel,
            Status = NotificationStatus.Queued,
            Recipient = recipient,
            Subject = subject,
            Body = body,
            OccurredAtUtc = _clock.UtcNow
        };
        await Logs.AddAsync(log, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return log.NotificationId;
    }
}
