using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using ATMET.AI.Api.Authentication;
using ATMET.AI.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using FluentValidation;
using ATMET.AI.Infrastructure.Extensions;

namespace ATMET.AI.Api.Extensions;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to configure API services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all ATMET AI API services (Azure AI, auth, CORS, rate limiting, Swagger, health checks, etc.).
    /// </summary>
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAzureAIServices(configuration);
        services.AddApiAuthentication(configuration);
        services.AddApiAuthorization();
        services.AddApiCors(configuration);
        services.AddApiRateLimiting(configuration);
        services.AddMemoryCache();
        services.AddApiHttpClients();
        services.AddApiHealthChecks();
        services.AddApiSwagger();
        services.AddApiJsonOptions();
        services.AddProblemDetails();
        services.AddApplicationInsightsTelemetry(options =>
        {
            options.ConnectionString = configuration["ApplicationInsights:ConnectionString"];
        });
        services.AddValidatorsFromAssemblyContaining<AzureAIOptions>();

        return services;
    }

    /// <summary>
    /// Configures API Key authentication (key passed in HTTP header).
    /// </summary>
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ApiKeyOptions>(configuration.GetSection(ApiKeyOptions.SectionName));

        services.AddAuthentication(ApiKeyAuthenticationOptions.DefaultScheme)
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationOptions.DefaultScheme,
                options =>
                {
                    var apiKeyOptions = configuration.GetSection(ApiKeyOptions.SectionName).Get<ApiKeyOptions>();
                    options.HeaderName = apiKeyOptions?.HeaderName ?? "X-Api-Key";
                    options.ValidKeys = (apiKeyOptions?.Keys ?? []).ToHashSet(StringComparer.Ordinal);
                });

        return services;
    }

    /// <summary>
    /// Configures authorization policies (ApiReader, ApiWriter). All valid API keys have full access.
    /// </summary>
    public static IServiceCollection AddApiAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy("ApiReader", policy =>
                policy.RequireAuthenticatedUser());
            options.AddPolicy("ApiWriter", policy =>
                policy.RequireAuthenticatedUser());
        });
        return services;
    }

    /// <summary>
    /// Configures CORS for SPA clients.
    /// </summary>
    public static IServiceCollection AddApiCors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AllowSPA", policy =>
            {
                policy.WithOrigins(configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            });
        });
        return services;
    }

    /// <summary>
    /// Configures rate limiting (fixed window and writes).
    /// </summary>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddFixedWindowLimiter("fixed", limiterOptions =>
            {
                limiterOptions.PermitLimit = configuration.GetValue("RateLimiting:PermitLimit", 100);
                limiterOptions.Window = TimeSpan.Parse(
                    configuration.GetValue("RateLimiting:Window", "00:01:00")!);
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = configuration.GetValue("RateLimiting:QueueLimit", 10);
            });

            options.AddFixedWindowLimiter("writes", limiterOptions =>
            {
                limiterOptions.PermitLimit = 30;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueLimit = 5;
            });
        });
        return services;
    }

    /// <summary>
    /// Adds HTTP clients (e.g. Azure AI with resilience).
    /// </summary>
    public static IServiceCollection AddApiHttpClients(this IServiceCollection services)
    {
        services.AddHttpClient("AzureAI")
            .AddStandardResilienceHandler();
        return services;
    }

    /// <summary>
    /// Adds health checks (self + Azure AI).
    /// </summary>
    public static IServiceCollection AddApiHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy())
            .AddAzureAIHealthCheck();
        return services;
    }

    /// <summary>
    /// Configures Swagger/OpenAPI with Bearer security.
    /// </summary>
    public static IServiceCollection AddApiSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new()
            {
                Title = "ATMET AI Service API",
                Version = "v1",
                Description = "Azure AI Foundry SDK Encapsulation API",
                Contact = new()
                {
                    Name = "ATMET AI Team",
                    Email = "ai-team@atmet.com"
                }
            });

            options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
            {
                Name = "X-Api-Key",
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = "API key passed in the X-Api-Key header."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("ApiKey", document, null)] = []
            });
        });
        return services;
    }

    /// <summary>
    /// Configures JSON options for HTTP APIs.
    /// </summary>
    public static IServiceCollection AddApiJsonOptions(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        return services;
    }
}
