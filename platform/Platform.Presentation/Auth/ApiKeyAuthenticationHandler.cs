// =============================================================================
// ApiKeyAuthenticationHandler  (Platform.Presentation.Auth)
// -----------------------------------------------------------------------------
// Custom AuthenticationHandler that delegates the actual cryptographic work
// to IApiKeyAuthorizer (in Platform.Infrastructure). On success it builds a
// ClaimsPrincipal carrying the tenant identity + scopes, AND populates the
// scoped ITenantContext so downstream EF queries are correctly scoped.
//
// HEADER CONTRACT  (also documented in IApiKeyAuthorizer)
//   X-App-Key:        public id
//   X-App-Timestamp:  ISO-8601 UTC, ±5 minutes
//   X-App-Signature:  HMAC-SHA256(secret, canonical) base64
//
//   canonical = "{X-App-Key}\n{X-App-Timestamp}\n{HTTP-VERB}\n{Path}\n{BodySha256-Hex}"
// =============================================================================

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Application.Abstractions.Auth;
using Platform.Domain.Tenancy;

namespace Platform.Presentation.Auth;

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions { }

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IApiKeyAuthorizer _authorizer;
    private readonly ITenantContext _tenant;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyAuthorizer authorizer,
        ITenantContext tenant)
        : base(options, logger, encoder)
    {
        _authorizer = authorizer;
        _tenant = tenant;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var req = Context.Request;
        var appKey    = req.Headers["X-App-Key"].FirstOrDefault();
        var signature = req.Headers["X-App-Signature"].FirstOrDefault();
        var timestamp = req.Headers["X-App-Timestamp"].FirstOrDefault();

        if (string.IsNullOrEmpty(appKey)) return AuthenticateResult.NoResult();

        // Compute SHA-256 of the request body. We must enable buffering so the
        // controller can re-read the body later.
        req.EnableBuffering();
        string bodyDigest = string.Empty;
        if (req.ContentLength > 0)
        {
            req.Body.Position = 0;
            using var sha = SHA256.Create();
            bodyDigest = Convert.ToHexString(await sha.ComputeHashAsync(req.Body));
            req.Body.Position = 0;
        }

        var result = await _authorizer.AuthorizeAsync(
            appKey:     appKey,
            signature:  signature,
            timestamp:  timestamp,
            httpMethod: req.Method,
            path:       req.Path,
            bodySha256: bodyDigest,
            clientIp:   Context.Connection.RemoteIpAddress?.ToString(),
            cancellationToken: Context.RequestAborted);

        if (result.IsFailure)
        {
            return AuthenticateResult.Fail(result.Error.Message);
        }

        // Stamp the tenant identity onto the per-request context so that
        // downstream EF query filters scope correctly.
        if (_tenant is Platform.Application.Abstractions.Tenancy.IMutableTenantContext mutable)
        {
            mutable.Resolve(result.Value.ClientId, result.Value.ClientCode);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, $"apikey:{result.Value.ApiKeyId}"),
            new(ClaimTypes.Name, result.Value.DisplayName),
            new("client_id", result.Value.ClientId.ToString()),
            new("auth_kind", "api-key")
        };
        foreach (var s in result.Value.Scopes) claims.Add(new Claim("scope", s));

        var identity = new ClaimsIdentity(claims, PlatformAuthSchemes.ApiKey);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, PlatformAuthSchemes.ApiKey);
        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        Response.Headers["WWW-Authenticate"] = $"{PlatformAuthSchemes.ApiKey} realm=\"canteen\"";
        return Task.CompletedTask;
    }
}
