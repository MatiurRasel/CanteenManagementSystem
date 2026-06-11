// =============================================================================
// AuthController  (Platform.Presentation.Controllers.Api.V1)
// -----------------------------------------------------------------------------
// Auth endpoints used by operator UIs and end-user clients.
//
//   POST /api/v1/auth/login           username/password  -> token pair
//   POST /api/v1/auth/refresh         refresh token       -> new token pair
//   POST /api/v1/auth/logout          revokes refresh
//   POST /api/v1/auth/change-password requires JWT
//   GET  /api/v1/auth/me              who am I  (JWT or API key)
// =============================================================================

using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Abstractions.Auth;
using Platform.Presentation.Auth;

namespace Platform.Presentation.Controllers.Api.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Operator / admin / end-user login.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthTokenPair), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 401)]
    public async Task<IActionResult> Login([FromBody] LoginDto request, CancellationToken cancellationToken)
    {
        var result = await _auth.LoginAsync(
            new LoginRequest(request.UserName, request.Password,
                HttpContext.Connection.RemoteIpAddress?.ToString()),
            cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : Unauthorized(new ProblemDetails { Title = "login_failed", Detail = result.Error.Message });
    }

    /// <summary>Trade a refresh token for a fresh access+refresh pair (rotation).</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshDto request, CancellationToken cancellationToken)
    {
        var result = await _auth.RefreshAsync(request.RefreshToken,
            HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return result.IsSuccess
            ? Ok(result.Value)
            : Unauthorized(new ProblemDetails { Title = "refresh_failed", Detail = result.Error.Message });
    }

    /// <summary>Revoke the supplied refresh token.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] RefreshDto request, CancellationToken cancellationToken)
    {
        await _auth.LogoutAsync(request.RefreshToken,
            HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        return NoContent();
    }

    /// <summary>Change own password. Revokes all refresh tokens for safety.</summary>
    [HttpPost("change-password")]
    [Authorize(AuthenticationSchemes = PlatformAuthSchemes.Bearer)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto request, CancellationToken cancellationToken)
    {
        var subClaim = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(subClaim, out var userId)) return Unauthorized();
        var result = await _auth.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword, cancellationToken);
        return result.IsSuccess
            ? Ok()
            : BadRequest(new ProblemDetails { Title = "change_password_failed", Detail = result.Error.Message });
    }

    /// <summary>Identity probe. Works under either auth scheme.</summary>
    [HttpGet("me")]
    [Authorize(Policy = "AuthenticatedAny")]
    public IActionResult Me()
    {
        var claims = User.Claims.Select(c => new { c.Type, c.Value });
        return Ok(new { authKind = User.FindFirst("auth_kind")?.Value ?? "bearer", name = User.Identity?.Name, claims });
    }
}

public sealed class LoginDto
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class RefreshDto
{
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class ChangePasswordDto
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
