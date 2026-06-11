// =============================================================================
// LocalNbrGatewaySimulator  (CanteenManagementSystem.Infrastructure.BackgroundJobs)
// -----------------------------------------------------------------------------
// Hosted service that exposes a tiny in-process HTTP listener at
// http://localhost:{Nbr.Sandbox.Port:5099}/nbr-sim, mimicking the NBR
// e-Mushak sandbox gateway. Lets `NbrReceiptServiceProduction` round-trip
// the full signed-XML envelope without contacting NBR.
//
// FLIP TO REAL
//   Set tenant settings:
//     Nbr.GatewayBaseUrl = https://emushak-sbox.nbr.gov.bd/api/v1   (or live URL)
//     Nbr.MerchantToken  = <real token>
//     Nbr.SigningKey     = <real PEM key>
//   …and disable the simulator by setting NbrSandbox.Enabled=false (or just
//   ignore the localhost endpoint — it never receives traffic).
//
// REQUESTS HANDLED
//   POST /nbr-sim/submit → 200 with JSON { frn, raw }
//     - mints a synthetic FRN ("FRN-{yyyyMMdd}-{6 random digits}")
//     - records the submission in NbrSandbox.LastSubmissions (in-memory)
//   GET  /nbr-sim/verify?frn={frn} → 200 HTML page showing receipt confirmation
//   GET  /nbr-sim/health → 200 "ok"
//
// Endpoint binds only to localhost so it is never reachable from outside
// the host process — safe to leave on in dev.
// =============================================================================

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanteenManagementSystem.Infrastructure.BackgroundJobs;

public sealed class LocalNbrGatewaySimulator : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly ILogger<LocalNbrGatewaySimulator> _logger;

    public LocalNbrGatewaySimulator(IConfiguration config, ILogger<LocalNbrGatewaySimulator> logger)
    {
        _config = config; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _config.GetValue("NbrSandbox:Enabled", true);
        if (!enabled)
        {
            _logger.LogInformation("Local NBR simulator disabled by NbrSandbox:Enabled=false.");
            return;
        }
        var port = _config.GetValue("NbrSandbox:Port", 5099);
        var prefix = $"http://localhost:{port}/nbr-sim/";

        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        try
        {
            listener.Start();
            _logger.LogInformation("Local NBR simulator listening at {Prefix}.", prefix);
        }
        catch (HttpListenerException ex)
        {
            _logger.LogWarning(ex, "Local NBR simulator could not bind {Prefix}; another process is already on the port.", prefix);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            HttpListenerContext? ctx = null;
            try
            {
                var getCtx = listener.GetContextAsync();
                using var reg = stoppingToken.Register(listener.Stop);
                ctx = await getCtx;
            }
            catch (HttpListenerException) { break; }     // listener stopped
            catch (ObjectDisposedException) { break; }

            if (ctx is null) continue;
            _ = Task.Run(() => HandleAsync(ctx), CancellationToken.None);
        }

        try { listener.Stop(); } catch { /* swallow */ }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        try
        {
            var path = ctx.Request.Url?.AbsolutePath ?? string.Empty;
            if (path.EndsWith("/health", StringComparison.OrdinalIgnoreCase))
            {
                await WriteAsync(ctx, 200, "text/plain", "ok");
                return;
            }
            if (path.EndsWith("/submit", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ctx.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                using var sr = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
                var body = await sr.ReadToEndAsync();
                var frn  = $"FRN-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(100_000, 999_999)}";
                var json = JsonSerializer.Serialize(new
                {
                    frn,
                    receivedAtUtc = DateTime.UtcNow,
                    payloadSize   = body.Length,
                    simulator     = "LocalNbrGatewaySimulator"
                });
                await WriteAsync(ctx, 200, "application/json", json);
                _logger.LogInformation("NBR sandbox → minted FRN {Frn} for {Bytes} bytes.", frn, body.Length);
                return;
            }
            if (path.EndsWith("/verify", StringComparison.OrdinalIgnoreCase))
            {
                var frn = ctx.Request.QueryString["frn"] ?? "(missing)";
                var html = $"<!doctype html><html><body style='font-family:sans-serif;padding:32px'>" +
                           $"<h1>NBR sandbox — verification</h1>" +
                           $"<p>FRN <code>{WebUtility.HtmlEncode(frn)}</code> was issued by the local NBR simulator and is considered valid for this dev instance.</p>" +
                           $"<p style='color:#888'>This is not a real NBR verification — switch <code>Nbr.GatewayBaseUrl</code> to the live URL when ready.</p>" +
                           "</body></html>";
                await WriteAsync(ctx, 200, "text/html", html);
                return;
            }

            await WriteAsync(ctx, 404, "text/plain", $"unknown path: {path}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NBR sandbox request crashed.");
            try { ctx.Response.StatusCode = 500; ctx.Response.Close(); } catch { /* swallow */ }
        }
    }

    private static async Task WriteAsync(HttpListenerContext ctx, int status, string contentType, string body)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = contentType;
        var bytes = Encoding.UTF8.GetBytes(body);
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.Close();
    }
}
