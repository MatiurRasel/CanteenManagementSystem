// =============================================================================
// DirectoryWebhookController  (CanteenManagementSystem.Presentation.Controllers.Api.V1)
// -----------------------------------------------------------------------------
// Inbound webhook for partner-pushed directory updates. School portal calls
// this whenever a student is added / updated / disabled — saves us waiting
// for the next pull cycle.
//
// AUTH: uses the SAME HMAC scheme as the rest of partner integrators
//       (X-App-Key + X-App-Timestamp + X-App-Signature) — see docs/API.md.
//       Auth handled by [Authorize(AuthenticationSchemes = "ApiKey")].
//
// PAYLOAD: a DirectoryDelta JSON. The partner sends the same shape the
//          existing ApiDirectorySource consumes, so push and pull share
//          one data model.
// =============================================================================

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Directory;

namespace CanteenManagementSystem.Presentation.Controllers.Api.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/webhooks/directory")]
[Authorize(AuthenticationSchemes = "ApiKey")]
[Produces("application/json")]
public sealed class DirectoryWebhookController : ControllerBase
{
    private readonly IDirectorySyncService _sync;

    public DirectoryWebhookController(IDirectorySyncService sync) => _sync = sync;

    /// <summary>
    /// Ingest a directory delta pushed by the school portal. The HMAC handler
    /// has already validated the signature + stamped the tenant context.
    /// </summary>
    /// <remarks>
    /// Body shape:
    /// <code>
    /// {
    ///   "students":  [ { "externalId": "...", "name": "...", ... } ],
    ///   "employees": [ { "externalId": "...", "name": "...", ... } ],
    ///   "isFullSnapshot": false
    /// }
    /// </code>
    /// Set <c>isFullSnapshot=true</c> to mark missing rows as inactive (use carefully).
    /// </remarks>
    [HttpPost]
    public async Task<ActionResult<DirectorySyncRunSummary>> Push(
        [FromBody] WebhookPayload payload, CancellationToken ct)
    {
        if (payload is null) return BadRequest(new { error = "Empty body." });

        var delta = new DirectoryDelta(
            Students:         payload.Students ?? Array.Empty<DirectoryStudent>(),
            Employees:        payload.Employees ?? Array.Empty<DirectoryEmployee>(),
            HighWatermarkUtc: DateTime.UtcNow,
            IsFullSnapshot:   payload.IsFullSnapshot);

        var run = await _sync.IngestAsync(delta, sourceLabel: "Webhook.Partner", ct);
        return Ok(run);
    }

    public sealed class WebhookPayload
    {
        public IReadOnlyList<DirectoryStudent>?  Students  { get; set; }
        public IReadOnlyList<DirectoryEmployee>? Employees { get; set; }
        public bool IsFullSnapshot { get; set; }
    }
}
