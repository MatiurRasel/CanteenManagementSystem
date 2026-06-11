using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace CanteenManagementSystem.Presentation.Controllers.Api.V1;

/// <summary>Lightweight liveness probe. Returns 200 OK with build info.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/health")]
[Produces("application/json")]
public sealed class HealthController : ControllerBase
{
    /// <summary>200 OK if the app is up.</summary>
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "ok",
        timestamp = DateTime.UtcNow,
        version = typeof(HealthController).Assembly.GetName().Version?.ToString() ?? "0.0.0"
    });
}
