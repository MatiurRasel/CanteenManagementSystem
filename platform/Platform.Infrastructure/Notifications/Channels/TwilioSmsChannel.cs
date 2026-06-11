// =============================================================================
// TwilioSmsChannel  (Infrastructure.Notifications.Channels)
// -----------------------------------------------------------------------------
// HttpClient-based call to Twilio's REST API for SMS. Avoid the official SDK
// to keep our deploy footprint tight; the REST contract is dead-simple.
//
// REQUIRED TENANT SETTINGS
//   Notifications.Sms.Provider        "Twilio" | "Sms365" (auto-routes)
//   Notifications.Sms.Twilio.AccountSid
//   Notifications.Sms.Twilio.AuthToken      (IsSecret)
//   Notifications.Sms.Twilio.From           "+8801XXXXXXXXX"
//
// FOR BD-ONLY ROUTING use the Sms365Channel registered alongside.
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

public sealed class TwilioSmsChannel : INotificationChannel
{
    public DomainChannel Channel => DomainChannel.Sms;

    private readonly HttpClient _http;
    private readonly ITenantSettings _settings;
    private readonly ILogger<TwilioSmsChannel> _logger;

    public TwilioSmsChannel(HttpClient http, ITenantSettings settings, ILogger<TwilioSmsChannel> logger)
    {
        _http = http; _settings = settings; _logger = logger;
    }

    public async Task<NotificationDeliveryResult> SendAsync(NotificationDispatchEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var sid = await _settings.GetAsync("Notifications.Sms.Twilio.AccountSid", null, cancellationToken);
        var tok = await _settings.GetAsync("Notifications.Sms.Twilio.AuthToken", null, cancellationToken);
        var from = await _settings.GetAsync("Notifications.Sms.Twilio.From", null, cancellationToken);

        if (string.IsNullOrEmpty(sid) || string.IsNullOrEmpty(tok) || string.IsNullOrEmpty(from))
        {
            return new NotificationDeliveryResult(false, null, "Twilio SMS not configured.");
        }

        try
        {
            var url = $"https://api.twilio.com/2010-04-01/Accounts/{sid}/Messages.json";
            var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{sid}:{tok}"));
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["To"] = envelope.Recipient,
                ["From"] = from,
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
            _logger.LogError(ex, "Twilio SMS failed for {To}", envelope.Recipient);
            return new NotificationDeliveryResult(false, null, ex.Message);
        }
    }
}
