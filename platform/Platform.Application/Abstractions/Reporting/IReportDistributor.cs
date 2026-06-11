// =============================================================================
// IReportDistributor  (Platform.Application.Abstractions.Reporting)
// -----------------------------------------------------------------------------
// Delivers a rendered report to its recipients. Implementation reads SMTP
// config from tenant settings (Notifications.Email.*) and attaches the bytes.
//
// Distribution targets are intentionally pluggable — future impls might:
//   * upload to a tenant's S3 bucket
//   * post to a Slack webhook
//   * write to a network share
// =============================================================================

namespace Platform.Application.Abstractions.Reporting;

public interface IReportDistributor
{
    /// <summary>Email a rendered report as an attachment to the given recipients.</summary>
    Task<DistributionResult> EmailAsync(
        IEnumerable<string> recipients,
        string subject,
        string htmlBody,
        RenderedReport attachment,
        CancellationToken cancellationToken = default);
}

public sealed record DistributionResult(bool Success, string? Detail);
