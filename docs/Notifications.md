# Notifications

## Architecture

```
[ command handler ] -> INotificationService.SendAsync(template, recipient, tokens)
                          |
                          v
                  NotificationService
                          |
                          +-- ITenantSettings.GetAsync("Notifications.Template.{key}.body")
                          +-- TemplateRenderer.Render(body, tokens)
                          +-- INSERT NotificationLog (status=Queued)
                          v
                  [ HTTP returns immediately ]


                  NotificationDispatcherService  (BackgroundService)
                          |
                          +-- SELECT TOP 25 NotificationLog WHERE status IN (Queued, Sending)
                          +-- for each: INotificationChannel.SendAsync(envelope)
                          +-- UPDATE NotificationLog (status=Sent/Failed/Queued for retry)
```

## Channels

| Channel | Default impl |
|---|---|
| Sms | `TwilioSmsChannel` — REST API |
| WhatsApp | `TwilioWhatsAppChannel` — REST API (same endpoint, `whatsapp:` prefix) |
| Email | `SmtpEmailChannel` — System.Net.Mail |

Add new channels by implementing `INotificationChannel` and registering it.

## Templates

Each template is a key plus subject/body/channel.

* **Database (recommended):** rows in `CanteenTenantSettings`:
  * `Notifications.Template.order.placed.subject`
  * `Notifications.Template.order.placed.body`
  * `Notifications.Template.order.placed.channel` (Sms / Email / WhatsApp)
* **appsettings.json fallback:** `Notifications:Template:order.placed:body`.
* **Code defaults:** `NotificationService.TemplateDefaults` dictionary.

## Token syntax

Bodies and subjects use `{a.b.c}` tokens replaced with deeply-nested values
from the tokens object. Anonymous objects, dictionaries, and POCOs all work.

```csharp
await notifications.SendAsync(NotificationTemplates.OrderReady, "01700000000",
    new { customer = "Anik", order = new { number = "ORD-241102-0001" } });
```

## Retry behaviour

* Up to 3 attempts per log row.
* On final failure: `Status=Failed`, `FailureReason` set.
* Dispatcher poll cadence: 2s busy, 10s idle.
