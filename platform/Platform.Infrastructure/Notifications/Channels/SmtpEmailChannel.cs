// =============================================================================
// SmtpEmailChannel  (Infrastructure.Notifications.Channels)
// -----------------------------------------------------------------------------
// SMTP via System.Net.Mail. Works with Gmail App Passwords, Mailgun, AWS SES
// in SMTP mode, or any tenant's own MX server.
//
// REQUIRED TENANT SETTINGS
//   Notifications.Email.Host          smtp.gmail.com
//   Notifications.Email.Port          587
//   Notifications.Email.UseSsl        true
//   Notifications.Email.Username
//   Notifications.Email.Password      (kept in IsSecret tenant setting)
//   Notifications.Email.From          "Canteen <no-reply@school.edu>"
// =============================================================================

using System.Net;
using System.Net.Mail;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Abstractions.Notifications;
using DomainChannel = Platform.Domain.Notifications.NotificationChannel;
using Microsoft.Extensions.Logging;

namespace Platform.Infrastructure.Notifications.Channels;

public sealed class SmtpEmailChannel : INotificationChannel
{
    public DomainChannel Channel => DomainChannel.Email;

    private readonly ITenantSettings _settings;
    private readonly ILogger<SmtpEmailChannel> _logger;

    public SmtpEmailChannel(ITenantSettings settings, ILogger<SmtpEmailChannel> logger)
    {
        _settings = settings; _logger = logger;
    }

    public async Task<NotificationDeliveryResult> SendAsync(NotificationDispatchEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var host    = await _settings.GetAsync("Notifications.Email.Host", null, cancellationToken);
        var port    = await _settings.GetIntAsync("Notifications.Email.Port", 587, cancellationToken);
        var useSsl  = await _settings.GetBoolAsync("Notifications.Email.UseSsl", true, cancellationToken);
        var user    = await _settings.GetAsync("Notifications.Email.Username", null, cancellationToken);
        var pwd     = await _settings.GetAsync("Notifications.Email.Password", null, cancellationToken);
        var from    = await _settings.GetAsync("Notifications.Email.From", "no-reply@example.com", cancellationToken) ?? "no-reply@example.com";

        if (string.IsNullOrEmpty(host))
        {
            return new NotificationDeliveryResult(false, null, "SMTP not configured (Notifications.Email.Host missing).");
        }

        try
        {
            using var client = new SmtpClient(host, port)
            {
                EnableSsl = useSsl,
                Credentials = (!string.IsNullOrEmpty(user)) ? new NetworkCredential(user, pwd) : null
            };
            using var message = new MailMessage
            {
                From = new MailAddress(from),
                Subject = envelope.Subject ?? "(no subject)",
                Body = envelope.Body,
                IsBodyHtml = envelope.Body.Contains("<html", StringComparison.OrdinalIgnoreCase)
            };
            message.To.Add(envelope.Recipient);
            await client.SendMailAsync(message, cancellationToken);
            return new NotificationDeliveryResult(true, $"smtp:{Guid.NewGuid():N}", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP send failed for {To}", envelope.Recipient);
            return new NotificationDeliveryResult(false, null, ex.Message);
        }
    }
}
