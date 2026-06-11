using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Platform.Presentation.Middleware;

/// Global exception → ProblemDetails (RFC 7807) middleware. Maps known
/// exception types to a structured JSON response with a correlation id, and
/// logs everything else as 500.
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            await WriteProblem(context, HttpStatusCode.BadRequest, "validation_error", "One or more validation errors occurred.",
                ex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }));
        }
        catch (UnauthorizedAccessException)
        {
            await WriteProblem(context, HttpStatusCode.Unauthorized, "unauthorized", "Authentication is required.");
        }
        catch (KeyNotFoundException ex)
        {
            await WriteProblem(context, HttpStatusCode.NotFound, "not_found", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            // Domain invariant violations surface as InvalidOperationException
            // until a richer DomainException type lands in P4.
            _logger.LogWarning(ex, "Domain invariant violation");
            await WriteProblem(context, HttpStatusCode.Conflict, "conflict", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Path}", context.Request.Path);
            await WriteProblem(context, HttpStatusCode.InternalServerError, "internal_error", "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblem(HttpContext context, HttpStatusCode status, string code, string detail, object? errors = null)
    {
        if (context.Response.HasStarted) return;

        // Content negotiation: browser navigations expect HTML, so just set the
        // status code and let UseStatusCodePagesWithReExecute render the branded
        // /error/{code} page upstream. API / fetch clients still get the
        // RFC 7807 ProblemDetails JSON.
        var path = context.Request.Path;
        var isApi = path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
                 || path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase);
        var accept = context.Request.Headers.Accept.ToString();
        var wantsHtml = !isApi
                        && !string.IsNullOrEmpty(accept)
                        && accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
        if (wantsHtml)
        {
            context.Response.Clear();
            context.Response.StatusCode = (int)status;
            return;
        }

        var problem = new ProblemDetails
        {
            Status = (int)status,
            Title = code,
            Detail = detail,
            Type = $"https://canteen.example/problems/{code}",
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;
        if (errors is not null) problem.Extensions["errors"] = errors;

        context.Response.Clear();
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}
