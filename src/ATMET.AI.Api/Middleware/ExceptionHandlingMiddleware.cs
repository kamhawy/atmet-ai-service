using ATMET.AI.Core.Exceptions;
using Azure;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace ATMET.AI.Api.Middleware;

/// <summary>
/// Global exception handling middleware for consistent error responses
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
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

        _logger.LogError(exception,
            "An unhandled exception occurred. TraceId: {TraceId}", traceId);

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

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
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

        // Add stack trace in development
        if (_environment.IsDevelopment() && exception.StackTrace is not null)
        {
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;
        }

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problemDetails, options));
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
