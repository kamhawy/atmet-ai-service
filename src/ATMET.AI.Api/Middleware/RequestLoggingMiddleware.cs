using System.Diagnostics;

namespace ATMET.AI.Api.Middleware;

/// <summary>
/// Middleware for logging HTTP requests and responses with performance metrics
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = context.TraceIdentifier;

        try
        {
            // Log request
            _logger.LogInformation(
                "HTTP {Method} {Path} started. RequestId: {RequestId}, User: {User}",
                context.Request.Method,
                context.Request.Path,
                requestId,
                context.User?.Identity?.Name ?? "Anonymous");

            await _next(context);

            stopwatch.Stop();

            // Log response
            _logger.LogInformation(
                "HTTP {Method} {Path} completed. RequestId: {RequestId}, StatusCode: {StatusCode}, Duration: {Duration}ms",
                context.Request.Method,
                context.Request.Path,
                requestId,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex,
                "HTTP {Method} {Path} failed. RequestId: {RequestId}, Duration: {Duration}ms",
                context.Request.Method,
                context.Request.Path,
                requestId,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }
}
