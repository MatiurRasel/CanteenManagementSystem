// =============================================================================
// TwilioWhatsAppChannel  (Infrastructure.Notifications.Channels)
// -----------------------------------------------------------------------------
// Twilio's WhatsApp Business API uses the same /Messages.json endpoint as SMS,
// but addresses are prefixed with "whatsapp:". Approved message templates can
// be used outside the 24-hour customer-care window.
//
// REQUIRED TENANT SETTINGS
//   Notifications.WhatsApp.Twilio.AccountSid
//   Notifications.WhatsApp.Twilio.AuthToken          (IsSecret)
//   Notifications.WhatsApp.Twilio.From               "whatsapp:+14155238886" (sandbox)
// =============================================================================

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Abstractions.Notifications;
using DomainChannel = Platform.Domain.Notifications.NotificationChannel;
using Microsoft.Extensions.Logging;

namespace Platform.Infrastructure.Notifications.Channels;

public sealed class TwilioWhatsAppChannel : INotificationChannel
{
    public DomainChannel Channel => DomainChannel.WhatsApp;

    private readonly HttpClient _http;
    private readonly ITenantSettings _settings;
    private readonly ILogger<TwilioWhatsAppChannel> _logger;

    public TwilioWhatsAppChannel(HttpClient http, ITenantSettings settings, ILogger<TwilioWhatsAppChannel> logger)
    { _http = http; _settings = settings; _logger = logger; }

    public async Task<NotificationDeliveryResult> SendAsync(NotificationDispatchEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var sid = await _settings.GetAsync("Notifications.WhatsApp.Twilio.AccountSid", null, cancellationToken);
        var tok = await _settings.GetAsync("Notifications.WhatsApp.Twilio.AuthToken", null, cancellationToken);
        var from = await _settings.GetAsync("Notifications.WhatsApp.Twilio.From", null, cancellationToken);
        if (string.IsNullOrEmpty(sid) || string.IsNullOrEmpty(tok) || string.IsNullOrEmpty(from))
        {
            return new NotificationDeliveryResult(false, null, "Twilio WhatsApp not configured.");
        }
        try
        {
            var url = $"https://api.twilio.com/2010-04-01/Accounts/{sid}/Messages.json";
            var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{sid}:{tok}"));
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["To"] = envelope.Recipient.StartsWith("whatsapp:") ? envelope.Recipient : "whatsapp:" + envelope.Recipient,
                ["From"] = from.StartsWith("whatsapp:") ? from : "whatsapp:" + from,
                ["Body"] = envelope.Body
            });
            var resp = await _http.SendAsync(req, cancellationToken);
            var body = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                var err = body.TryGetProperty("message", out var m) ? m.GetString() : resp.ReasonPhrase;
                return new NotificationDeliveryResult(false, null, err);
            }
            return new NotificationDeliveryResult(true, body.GetProperty("sid").GetString(), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WhatsApp send failed for {To}", envelope.Recipient);
            return new NotificationDeliveryResult(false, null, ex.Message);
        }
    }
}
