using ATMET.AI.Core.Exceptions;
using Azure;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace ATMET.AI.Api.Middleware;

/// <summary>
/// Global exception handling middleware for consistent error responses and Application Insights exception telemetry.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;
    private readonly TelemetryClient? _telemetryClient;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment,
        IServiceProvider serviceProvider)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
        _telemetryClient = serviceProvider.GetService<TelemetryClient>();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;
        var path = context.Request.Path.Value ?? string.Empty;
        var method = context.Request.Method;

        var (statusCode, title, detail) = exception switch
        {
            ATMET.AI.Core.Exceptions.ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                "Validation Error",
                validationEx.Message
            ),
            FluentValidation.ValidationException fluentEx => (
                HttpStatusCode.BadRequest,
                "Validation Error",
                fluentEx.Message
            ),
            NotFoundException notFoundEx => (
                HttpStatusCode.NotFound,
                "Resource Not Found",
                notFoundEx.Message
            ),
            ForbiddenException forbiddenEx => (
                HttpStatusCode.Forbidden,
                "Forbidden",
                forbiddenEx.Message
            ),
            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                "Unauthorized",
                "You are not authorized to access this resource"
            ),
            RequestFailedException azureEx => MapAzureException(azureEx),
            _ => (
                HttpStatusCode.InternalServerError,
                "Internal Server Error",
                _environment.IsDevelopment()
                    ? exception.Message
                    : "An error occurred while processing your request"
            )
        };

        var status = (int)statusCode;
        var telemetryProperties = BuildTelemetryProperties(
            traceId, path, method, status, exception, title);

        _telemetryClient?.TrackException(exception, telemetryProperties);

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = traceId,
            ["RequestPath"] = path,
            ["RequestMethod"] = method,
            ["StatusCode"] = status,
            ["ExceptionType"] = exception.GetType().FullName,
            ["ProblemTitle"] = title
        }))
        {
            _logger.LogError(
                exception,
                "Request failed: {Method} {Path} -> {StatusCode} ({ExceptionType}). {Title}. TraceId: {TraceId}",
                method,
                path,
                status,
                exception.GetType().Name,
                title,
                traceId);
        }

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
            Extensions =
            {
                ["traceId"] = traceId,
                ["timestamp"] = DateTimeOffset.UtcNow
            }
        };

        if (exception is FluentValidation.ValidationException fluentValidationEx)
        {
            problemDetails.Extensions["errors"] = fluentValidationEx.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }

        if (_environment.IsDevelopment() && exception.StackTrace is not null)
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problemDetails, options));
    }

    private static Dictionary<string, string> BuildTelemetryProperties(
        string traceId,
        string path,
        string method,
        int statusCode,
        Exception exception,
        string title)
    {
        var props = new Dictionary<string, string>
        {
            ["TraceId"] = traceId,
            ["RequestPath"] = path,
            ["RequestMethod"] = method,
            ["StatusCode"] = statusCode.ToString(),
            ["ExceptionType"] = exception.GetType().FullName ?? exception.GetType().Name,
            ["ProblemTitle"] = title
        };

        if (exception.InnerException is not null)
            props["InnerExceptionType"] = exception.InnerException.GetType().FullName ?? "";

        if (exception is RequestFailedException rfe)
        {
            props["AzureStatus"] = rfe.Status.ToString();
            if (!string.IsNullOrEmpty(rfe.ErrorCode))
                props["AzureErrorCode"] = rfe.ErrorCode;
        }

        return props;
    }

    private (HttpStatusCode statusCode, string title, string detail) MapAzureException(
        RequestFailedException exception)
    {
        var safeDetail = _environment.IsDevelopment()
            ? exception.Message
            : "An error occurred communicating with Azure AI services";

        return exception.Status switch
        {
            400 => (HttpStatusCode.BadRequest, "Bad Request", exception.Message),
            401 => (HttpStatusCode.Unauthorized, "Unauthorized", "Invalid or missing credentials"),
            403 => (HttpStatusCode.Forbidden, "Forbidden", "Insufficient permissions"),
            404 => (HttpStatusCode.NotFound, "Not Found", exception.Message),
            409 => (HttpStatusCode.Conflict, "Conflict", exception.Message),
            429 => (HttpStatusCode.TooManyRequests, "Too Many Requests", "Rate limit exceeded. Please retry after a delay."),
            500 => (HttpStatusCode.InternalServerError, "Azure Service Error", safeDetail),
            503 => (HttpStatusCode.ServiceUnavailable, "Service Unavailable", "Azure service temporarily unavailable"),
            _ => (HttpStatusCode.InternalServerError, "Azure Error", safeDetail)
        };
    }
}
