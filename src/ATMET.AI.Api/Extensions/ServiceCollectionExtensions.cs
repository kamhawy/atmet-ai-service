using ATMET.AI.Api.Authentication;
using ATMET.AI.Infrastructure.Configuration;
using ATMET.AI.Infrastructure.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

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
    /// Configures Swagger/OpenAPI with API key security and enhanced documentation.
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
                Description = """
                    REST API encapsulating Azure AI Foundry SDK capabilities.

                    **Features:**
                    - **Agents**: Create and manage persistent AI agents with threads, messages, runs, and file uploads
                    - **Chat**: Azure OpenAI chat completions (sync and streaming)
                    - **Deployments**: List and inspect model deployments (GPT-4, GPT-4o, etc.)
                    - **Connections**: Azure resource connections (OpenAI, AI Search)
                    - **Datasets**: Upload and manage datasets for training/inference
                    - **Indexes**: Azure AI Search index definitions

                    **Authentication**: All endpoints require an API key in the `X-Api-Key` header.
                    """,
                Contact = new() { Name = "ATMET AI Team", Email = "ai-team@atmet.ai" }
            });

            // Include XML comments from API and Core assemblies
            var apiXmlPath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
            if (File.Exists(apiXmlPath))
                options.IncludeXmlComments(apiXmlPath);

            var coreAssembly = typeof(ATMET.AI.Core.Models.Requests.CreateAgentRequest).Assembly;
            var coreXmlPath = Path.Combine(AppContext.BaseDirectory, $"{coreAssembly.GetName().Name}.xml");
            if (File.Exists(coreXmlPath))
                options.IncludeXmlComments(coreXmlPath);

            options.OrderActionsBy(api => api.RelativePath ?? "");

            options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
            {
                Name = "X-Api-Key",
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Description = "API key for authentication. Pass in the X-Api-Key header."
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
