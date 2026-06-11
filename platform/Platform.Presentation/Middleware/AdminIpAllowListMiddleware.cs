// =============================================================================
// AdminIpAllowListMiddleware  (Platform.Presentation.Middleware)
// -----------------------------------------------------------------------------
// Optional per-tenant IP allow-list for /admin/* and /sysadmin/* routes.
// When configured, requests whose remote IP is not on the list receive HTTP 403.
//
// CONFIGURATION
//   Tenant setting `Admin.IpAllowList` — comma-separated list of:
//     * single IPv4/IPv6 addresses     e.g. "203.0.113.42"
//     * CIDR ranges                    e.g. "203.0.113.0/24"  "2001:db8::/32"
//   Empty / missing setting = no enforcement (default — keeps dev simple).
//
// REVERSE PROXY
//   Trusts Forwarded-For headers ONLY when the request is from a configured
//   `Network.ForwardedForKnownNetworks` CIDR. Otherwise honours RemoteIpAddress
//   directly. (ASP.NET Core's ForwardedHeaders middleware should already be on
//   the pipeline in production.)
//
// AUDIT
//   Every 403 records a "Admin.IpDenied" audit entry with the source IP + path,
//   so a tenant admin can review blocked attempts.
// =============================================================================

using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Platform.Application.Abstractions.Audit;
using Platform.Application.Abstractions.Configuration;

namespace Platform.Presentation.Middleware;

public sealed class AdminIpAllowListMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AdminIpAllowListMiddleware> _logger;

    public AdminIpAllowListMiddleware(RequestDelegate next, ILogger<AdminIpAllowListMiddleware> logger)
    {
        _next = next; _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantSettings settings, IAuditTrail audit)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!IsAdminPath(path))
        {
            await _next(context);
            return;
        }

        var raw = await settings.GetAsync("Admin.IpAllowList", defaultValue: null, context.RequestAborted);
        if (string.IsNullOrWhiteSpace(raw))
        {
            await _next(context);
            return;
        }

        var clientIp = context.Connection.RemoteIpAddress;
        if (clientIp is null)
        {
            await Deny(context, audit, "(unknown)", path);
            return;
        }

        var entries = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (entries.Any(entry => MatchesEntry(entry, clientIp)))
        {
            await _next(context);
            return;
        }

        await Deny(context, audit, clientIp.ToString(), path);
    }

    private static bool IsAdminPath(string path)
        => path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/sysadmin", StringComparison.OrdinalIgnoreCase);

    private static bool MatchesEntry(string entry, IPAddress clientIp)
    {
        if (entry.Contains('/'))
        {
            return TryParseCidr(entry, out var network, out var prefix) && InNetwork(clientIp, network, prefix);
        }
        return IPAddress.TryParse(entry, out var addr) && addr.Equals(NormalizeIp(clientIp));
    }

    private static IPAddress NormalizeIp(IPAddress ip)
        => ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;

    private static bool TryParseCidr(string raw, out IPAddress network, out int prefixLength)
    {
        network = IPAddress.Any; prefixLength = 0;
        var parts = raw.Split('/', 2);
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var addr) ||
            !int.TryParse(parts[1], out var prefix))
            return false;
        network = addr;
        prefixLength = prefix;
        return true;
    }

    private static bool InNetwork(IPAddress client, IPAddress network, int prefixLength)
    {
        client = NormalizeIp(client);
        if (client.AddressFamily != network.AddressFamily) return false;

        var clientBytes  = client.GetAddressBytes();
        var networkBytes = network.GetAddressBytes();
        var fullBytes = prefixLength / 8;
        var remainder = prefixLength % 8;

        for (int i = 0; i < fullBytes; i++)
        {
            if (clientBytes[i] != networkBytes[i]) return false;
        }
        if (remainder == 0) return true;
        var mask = (byte)(0xff << (8 - remainder));
        return (clientBytes[fullBytes] & mask) == (networkBytes[fullBytes] & mask);
    }

    private async Task Deny(HttpContext context, IAuditTrail audit, string ip, string path)
    {
        _logger.LogWarning("Admin IP allow-list denied {Ip} for {Path}", ip, path);
        try
        {
            await audit.RecordAsync(
                action: "Admin.IpDenied",
                entityType: "AdminRoute",
                entityId: null,
                payload: new { ip, path, ua = context.Request.Headers.UserAgent.ToString() },
                cancellationToken: context.RequestAborted);
        }
        catch { /* never fail the deny because audit failed */ }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsync(
            "Your network is not on the administrator allow-list for this tenant. " +
            "Ask your tenant admin to add your IP to the Admin.IpAllowList setting.",
            context.RequestAborted);
    }
}
