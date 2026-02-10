using ATMET.AI.Api.Endpoints;
using ATMET.AI.Api.Middleware;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;

namespace ATMET.AI.Api.Extensions;

/// <summary>
/// Extension methods for <see cref="WebApplication"/> to configure the pipeline and endpoints.
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Configures the ATMET AI API middleware pipeline (exception handling, logging, Swagger, security headers, auth, rate limiting).
    /// </summary>
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();

        // OpenAPI document (Swashbuckle) — required for Scalar and optional Swagger UI
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "ATMET AI Service API v1");
            options.RoutePrefix = "swagger";
        });
        // Scalar API reference UI (modern, production-ready docs)
        app.MapScalarApiReference(options =>
        {
            options.WithTitle("ATMET AI Service API");
            options.WithOpenApiRoutePattern("/swagger/v1/swagger.json");
        });

        app.UseSecurityHeaders();
        app.UseHttpsRedirection();
        app.UseCors("AllowSPA");
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        return app;
    }

    /// <summary>
    /// Adds security headers to each response.
    /// </summary>
    public static WebApplication UseSecurityHeaders(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Append("X-Frame-Options", "DENY");
            context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
            context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

            var path = context.Request.Path.Value ?? "";
            if (path.StartsWith("/scalar", StringComparison.OrdinalIgnoreCase))
            {
                // Scalar API docs use inline scripts/styles and fonts from fonts.scalar.com
                context.Response.Headers.Append("Content-Security-Policy",
                    "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; font-src 'self' https://fonts.scalar.com");
            }
            else
            {
                context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'");
            }

            await next();
        });
        return app;
    }

    /// <summary>
    /// Maps health check and API endpoints.
    /// </summary>
    public static WebApplication MapApiEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });

        var apiGroup = app.MapGroup("/api/v1")
            .RequireAuthorization("ApiReader")
            .RequireRateLimiting("fixed");

        AgentsEndpoints.MapEndpoints(apiGroup);
        DeploymentsEndpoints.MapEndpoints(apiGroup);
        ConnectionsEndpoints.MapEndpoints(apiGroup);
        DatasetsEndpoints.MapEndpoints(apiGroup);
        IndexesEndpoints.MapEndpoints(apiGroup);
        ChatEndpoints.MapEndpoints(apiGroup);

        app.MapGet("/", () => Results.Ok(new
        {
            Service = "ATMET AI Service",
            Version = "1.0.0",
            Status = "Running",
            Documentation = "/scalar",
            Health = "/health"
        })).WithTags("Info");

        return app;
    }
}
