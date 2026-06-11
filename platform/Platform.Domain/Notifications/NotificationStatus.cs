namespace Platform.Domain.Notifications;

public enum NotificationStatus
{
    Queued = 1,
    Sending = 2,
    Sent = 3,
    Failed = 4,
    Skipped = 5
}
