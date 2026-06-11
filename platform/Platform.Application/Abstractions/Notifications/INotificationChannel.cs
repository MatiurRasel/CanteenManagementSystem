using Platform.Domain.Notifications;

namespace Platform.Application.Abstractions.Notifications;

/// <summary>
/// One implementation per delivery transport: SMS, Email, WhatsApp...
/// The dispatcher picks the channel matching the NotificationLog.Channel.
/// </summary>
public interface INotificationChannel
{
    NotificationChannel Channel { get; }
    Task<NotificationDeliveryResult> SendAsync(NotificationDispatchEnvelope envelope, CancellationToken cancellationToken = default);
}

public sealed record NotificationDispatchEnvelope(
    long NotificationId,
    string Recipient,
    string? Subject,
    string Body,
    IDictionary<string, string>? Headers = null);

public sealed record NotificationDeliveryResult(bool Success, string? ProviderId, string? FailureReason);
