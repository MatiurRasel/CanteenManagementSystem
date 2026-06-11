// =============================================================================
// SmtpReportDistributor  (Platform.Infrastructure.Reporting)
// -----------------------------------------------------------------------------
// SMTP with attachment support, reusing the existing per-tenant
// Notifications.Email.* settings (same keys SmtpEmailChannel reads).
//
// FAILURE MODE
//   Returns DistributionResult.Success=false rather than throwing — the
//   scheduler catches it and stamps LastError on the schedule row.
// =============================================================================

using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Microsoft.Extensions.Logging;
using Platform.Application.Abstractions.Configuration;
using Platform.Application.Abstractions.Reporting;

namespace Platform.Infrastructure.Reporting;

public sealed class SmtpReportDistributor : IReportDistributor
{
    private readonly ITenantSettings _settings;
    private readonly ILogger<SmtpReportDistributor> _logger;

    public SmtpReportDistributor(ITenantSettings settings, ILogger<SmtpReportDistributor> logger)
    {
        _settings = settings; _logger = logger;
    }

    public async Task<DistributionResult> EmailAsync(
        IEnumerable<string> recipients, string subject, string htmlBody,
        RenderedReport attachment, CancellationToken cancellationToken = default)
    {
        var clean = (recipients ?? Array.Empty<string>())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (clean.Count == 0) return new DistributionResult(false, "No recipients.");

        var host    = await _settings.GetAsync("Notifications.Email.Host", null, cancellationToken);
        var port    = await _settings.GetIntAsync("Notifications.Email.Port", 587, cancellationToken);
        var useSsl  = await _settings.GetBoolAsync("Notifications.Email.UseSsl", true, cancellationToken);
        var user    = await _settings.GetAsync("Notifications.Email.Username", null, cancellationToken);
        var pwd     = await _settings.GetAsync("Notifications.Email.Password", null, cancellationToken);
        var from    = await _settings.GetAsync("Notifications.Email.From", "no-reply@example.com", cancellationToken) ?? "no-reply@example.com";

        if (string.IsNullOrWhiteSpace(host))
            return new DistributionResult(false, "SMTP not configured (Notifications.Email.Host missing).");

        try
        {
            using var client = new SmtpClient(host, port)
            {
                EnableSsl = useSsl,
                Credentials = !string.IsNullOrEmpty(user) ? new NetworkCredential(user, pwd) : null,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            using var message = new MailMessage { From = new MailAddress(from), Subject = subject, Body = htmlBody, IsBodyHtml = true };
            foreach (var r in clean) message.To.Add(r);

            using var ms = new MemoryStream(attachment.Bytes, writable: false);
            using var msAttachment = new Attachment(ms, attachment.SuggestedFileName, attachment.MimeType);
            // Use a ContentDisposition so mail clients show a proper filename.
            msAttachment.ContentDisposition!.FileName = attachment.SuggestedFileName;
            msAttachment.ContentDisposition.DispositionType = DispositionTypeNames.Attachment;
            message.Attachments.Add(msAttachment);

            await client.SendMailAsync(message, cancellationToken);
            return new DistributionResult(true, $"Sent to {clean.Count} recipient(s) — {attachment.Bytes.Length} bytes.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP attachment send failed (subject='{Subject}')", subject);
            return new DistributionResult(false, ex.Message);
        }
    }
}
