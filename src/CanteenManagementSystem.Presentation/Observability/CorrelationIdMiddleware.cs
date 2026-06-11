// =============================================================================
// CorrelationIdMiddleware  (CanteenManagementSystem.Presentation.Observability)
// -----------------------------------------------------------------------------
// Reads the inbound `X-Correlation-ID` header (any of the common aliases). If
// the caller didn't send one, we mint a short ULID-ish id. The value is:
//   * pushed into Serilog's LogContext as `CorrelationId`
//   * echoed back on the response under the same header so the caller can pivot
//   * stamped onto HttpContext.Items["CorrelationId"] for ad-hoc reads
//
// Order: register BEFORE UseSerilogRequestLogging so the request-summary line
// also carries CorrelationId.
// =============================================================================

using Serilog.Context;

namespace CanteenManagementSystem.Presentation.Observability;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName    = "X-Correlation-ID";
    public const string LegacyHeader1 = "X-Request-ID";
    public const string LegacyHeader2 = "X-Correlation-Id";   // case-insensitive but kept here for explicit hits
    public const string ContextKey    = "CorrelationId";

    private readonly RequestDelegate _next;
    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var id = ResolveOrMint(context);
        context.Items[ContextKey] = id;

        context.Response.OnStarting(() =>
        {
            // Echo back to the caller so partner integrations can correlate.
            if (!context.Response.Headers.ContainsKey(HeaderName))
                context.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(ContextKey, id))
        {
            await _next(context);
        }
    }

    private static string ResolveOrMint(HttpContext context)
    {
        foreach (var name in new[] { HeaderName, LegacyHeader1, LegacyHeader2 })
        {
            if (context.Request.Headers.TryGetValue(name, out var v) && !string.IsNullOrWhiteSpace(v))
            {
                var raw = v.ToString().Trim();
                // Cap length so a malicious caller can't flood log lines.
                return raw.Length > 64 ? raw[..64] : raw;
            }
        }
        // Mint a compact id — 12 base32 chars ≈ 60 bits, fits a row in Seq nicely.
        Span<byte> bytes = stackalloc byte[8];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
