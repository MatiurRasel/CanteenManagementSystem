using Asp.Versioning;
using Platform.Application.Abstractions.Payments;
using CanteenManagementSystem.Domain.Enums;
using Platform.Domain.Payments;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Presentation.Controllers.Api.V1;

/// <summary>
/// Payments API: start a recharge, receive gateway callbacks. Webhooks are
/// idempotent — duplicate calls return the same transaction without
/// re-crediting the wallet.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentOrchestrator _orchestrator;

    public PaymentsController(IPaymentOrchestrator orchestrator) => _orchestrator = orchestrator;

    /// <summary>Begin a wallet recharge. Returns a redirect URL.</summary>
    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] StartRechargeRequest request, CancellationToken cancellationToken)
    {
        var result = await _orchestrator.StartRechargeAsync(
            request.UserId, request.UserType.ToString(), request.Amount, request.Method,
            request.CustomerName, request.CustomerPhone, request.CustomerEmail, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new ProblemDetails { Title = "payment_start_failed", Detail = result.Error.Message });
    }

    /// <summary>Generic callback endpoint shape; one per gateway is wired below.</summary>
    [HttpPost("{gateway}/callback")]
    [HttpGet("{gateway}/callback")]
    public async Task<IActionResult> Callback(string gateway, [FromQuery(Name = "ref")] string transactionRef, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PaymentMethod>(gateway, ignoreCase: true, out var method))
        {
            return BadRequest(new ProblemDetails { Title = "unknown_gateway", Detail = gateway });
        }

        // Collect every query + form key as a flat dictionary for the gateway
        // impl to interpret. Webhook bodies vary widely between providers.
        var payload = Request.Query.ToDictionary(k => k.Key, v => v.Value.ToString());
        if (Request.HasFormContentType)
        {
            foreach (var kv in await Request.ReadFormAsync(cancellationToken))
            {
                payload[kv.Key] = kv.Value.ToString();
            }
        }

        var result = await _orchestrator.HandleCallbackAsync(method, transactionRef, payload, cancellationToken);
        if (result.IsFailure) return BadRequest(new ProblemDetails { Title = "payment_callback_failed", Detail = result.Error.Message });

        // Redirect the user back into the app with a friendly toast.
        var status = result.Value.Status == PaymentStatus.Succeeded ? "success" : "failed";
        var flash  = result.Value.Status == PaymentStatus.Succeeded ? "Recharge successful" : (result.Value.GatewayMessage ?? "Recharge failed");
        return Redirect($"/?flash={Uri.EscapeDataString(flash)}&flashType={status}");
    }
}

public sealed class StartRechargeRequest
{
    public string UserId { get; set; } = string.Empty;
    public CanteenUserType UserType { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
}
