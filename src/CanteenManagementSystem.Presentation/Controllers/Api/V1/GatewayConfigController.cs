using Asp.Versioning;
using Platform.Application.Abstractions.Payments;
using Platform.Domain.Payments;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Presentation.Controllers.Api.V1;

/// <summary>
/// Admin endpoints for managing the two gateway tables:
/// CanteenPaymentGatewayConfigs + CanteenGatewayChannels.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/gateways")]
[Produces("application/json")]
public sealed class GatewayConfigController : ControllerBase
{
    private readonly IGatewayConfigService _service;

    public GatewayConfigController(IGatewayConfigService service) => _service = service;

    /// <summary>List all configured gateways for the current tenant.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PaymentGatewayConfig>>> List(CancellationToken cancellationToken)
        => Ok(await _service.ListAsync(cancellationToken));

    /// <summary>Get config detail by payment method.</summary>
    [HttpGet("by-method/{method}")]
    public async Task<ActionResult<PaymentGatewayConfig>> ByMethod(PaymentMethod method, CancellationToken cancellationToken)
    {
        var cfg = await _service.GetConfigAsync(method, cancellationToken);
        return cfg is null ? NotFound() : Ok(cfg);
    }

    /// <summary>Flip the active environment (sandbox &lt;-&gt; live).</summary>
    [HttpPost("{configId:int}/set-sandbox")]
    public async Task<IActionResult> SetSandbox(int configId, [FromQuery] bool isSandbox, CancellationToken cancellationToken)
        => await _service.SetSandboxAsync(configId, isSandbox, cancellationToken) ? Ok() : NotFound();

    /// <summary>Toggle the master enabled flag.</summary>
    [HttpPost("{configId:int}/toggle")]
    public async Task<IActionResult> Toggle(int configId, CancellationToken cancellationToken)
        => await _service.ToggleEnabledAsync(configId, cancellationToken) ? Ok() : NotFound();

    /// <summary>Save credentials for either Live (isLive=true) or Sandbox env.</summary>
    [HttpPost("{configId:int}/credentials/{env}")]
    public async Task<IActionResult> SaveCredentials(int configId, string env, [FromBody] GatewayCredentialsInput input, CancellationToken cancellationToken)
    {
        var isLive = string.Equals(env, "live", StringComparison.OrdinalIgnoreCase);
        return await _service.SaveCredentialsAsync(configId, isLive, input, cancellationToken) ? Ok() : NotFound();
    }

    /// <summary>List active channels under a gateway.</summary>
    [HttpGet("{configId:int}/channels")]
    public async Task<ActionResult<IReadOnlyList<GatewayChannel>>> Channels(int configId, CancellationToken cancellationToken)
        => Ok(await _service.GetChannelsAsync(configId, cancellationToken));

    /// <summary>Insert or update a channel under a gateway.</summary>
    [HttpPost("{configId:int}/channels")]
    public async Task<ActionResult<object>> UpsertChannel(int configId, [FromBody] GatewayChannelInput input, CancellationToken cancellationToken)
    {
        var id = await _service.UpsertChannelAsync(configId, input, cancellationToken);
        return id > 0 ? Ok(new { channelId = id }) : NotFound();
    }

    /// <summary>Delete a channel row.</summary>
    [HttpDelete("channels/{channelId:int}")]
    public async Task<IActionResult> DeleteChannel(int channelId, CancellationToken cancellationToken)
        => await _service.DeleteChannelAsync(channelId, cancellationToken) ? NoContent() : NotFound();

    /// <summary>Record the outcome of a sandbox / live ping test.</summary>
    [HttpPost("{configId:int}/test-result")]
    public async Task<IActionResult> RecordTest(int configId, [FromQuery] bool success, [FromQuery] string? message, CancellationToken cancellationToken)
        => await _service.RecordTestAsync(configId, success, message, cancellationToken) ? Ok() : NotFound();
}
