using ATMET.AI.Api.Authentication;
using ATMET.AI.Api.OpenApi;
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
        services.AddSupabaseServices(configuration);
        services.AddApiAuthentication(configuration);
        services.AddApiAuthorization();
        services.AddApiCors(configuration);
        services.AddApiRateLimiting(configuration);
        services.AddMemoryCache();
        services.AddOutputCache(options =>
        {
            options.AddPolicy("PortalCatalog", builder =>
                builder.Expire(TimeSpan.FromMinutes(5)).Tag("portal-catalog"));
        });
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
            .AddAzureAIHealthCheck()
            .AddSupabaseHealthCheck();
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
                    National-scale orchestration API for **ATMET AI** (أتمت): **Azure AI Foundry** capabilities plus a **Supabase-backed citizen portal** surface.

                    ---

                    ### Base path and versioning
                    All integration endpoints are under **`/api/v1`**. The document version (`v1`) tracks the OpenAPI description; breaking HTTP changes should bump this path in future releases.

                    ---

                    ### Product surfaces (who uses what)
                    | Area | Tags | Purpose |
                    |------|------|---------|
                    | **Azure AI Foundry proxy** | Agents, Chat, Deployments, Connections, Datasets, Indexes | Manage persistent agents, chat completions, project deployments/connections, datasets, and search index registrations against the configured Foundry project. |
                    | **Citizen portal (MUBASHIR)** | Portal - …, Portal Chat | CRUD and workflows for **services**, **cases** (applications), **conversations**, **documents**, **forms**, **workflow steps**, **activity**, and **SSE agent chat**. Data is loaded from **PostgreSQL (Supabase)** via the service layer. |

                    ---

                    ### Authentication
                    - **`X-Api-Key`** (required on **`/api/v1/*`**): shared secret issued by the platform team. Keys are configured server-side (`ApiKeys:Keys` in configuration). There is no OAuth2 flow on this API today—treat the key like a confidential integration credential.
                    - **Authorization policies** in code are named `ApiReader` (read) and `ApiWriter` (mutations). **Currently any valid API key satisfies both**; the split exists for future key scoping.

                    ---

                    ### Portal context headers (citizen flows)
                    Portal routes additionally rely on **caller-supplied identity and tenant headers** (UUID strings). Your BFF or trusted client should set these after your own session/auth validation:
                    | Header | When | Meaning |
                    |--------|------|---------|
                    | `X-Portal-User-Id` | Most portal mutations and case-scoped reads | Authenticated citizen / profile id. |
                    | `X-Portal-Entity-Id` | Catalog list, listing cases/conversations, portal chat | Tenant (government entity) id for multi-tenant isolation. |
                    | `X-Portal-Language` | Optional on portal chat | `en` or `ar` for bilingual UX. |

                    OpenAPI lists these per operation where applicable. **Service-by-id** catalog reads (`GET /portal/services/{serviceId}` and workflow) intentionally use **only route parameters** today.

                    ---

                    ### Rate limiting and caching
                    - **`/api/v1/*`** uses a **fixed-window** rate limiter (`429 Too Many Requests` when exceeded). Limits are configurable under `RateLimiting` in application settings.
                    - **Portal service catalog** GET endpoints may be **output-cached** briefly for performance (`PortalCatalog` policy).

                    ---

                    ### Errors and validation
                    - **FluentValidation** failures return **`400`** with a **validation problem** payload (RFC 7807-style `application/problem+json` where configured).
                    - **Not found** and domain errors map to **`404`** / **`400`** / **`500`** depending on exception type—see individual operation responses.
                    - **Unauthorized** (`401`) when the API key is missing or invalid.

                    ---

                    ### Streaming (SSE)
                    These endpoints return **`text/event-stream`** (Server-Sent Events). Each event is typically one line: `data: {json}` and the stream terminates with **`data: [DONE]`**:
                    - `POST /api/v1/chat/completions/stream`
                    - `POST /api/v1/portal/conversations/{conversationId}/chat`

                    Clients should disable buffering on reverse proxies (`X-Accel-Buffering: no` is set by the API for streaming routes).

                    ---

                    ### Multipart uploads
                    Use **`multipart/form-data`** for:
                    - Agent file upload: `POST /api/v1/agents/files`
                    - Dataset file/folder uploads under `/api/v1/datasets/upload/*`
                    - Portal case document upload: `POST /api/v1/portal/cases/{caseId}/documents`

                    ---

                    ### Health and documentation
                    - **Liveness / readiness**: `GET /health`, `GET /health/ready`, `GET /health/live`
                    - **OpenAPI JSON**: `GET /swagger/v1/swagger.json`
                    - **Interactive docs**: **`/scalar`** (recommended) or **`/swagger`**
                    - **Root**: `GET /` returns a small JSON service card (no API key).

                    ---

                    ### Schema and naming
                    JSON uses **camelCase** property names. **Enums** are serialized as strings where applicable. Nullable fields are omitted on write when null (`JsonSerializer` defaults).

                    **Request/response DTOs** are documented via XML comments on the Core models; cross-check tag summaries above for business context.
                    """,
                Contact = new() { Name = "ATMET AI Team", Email = "ai-team@atmet.ai" }
            });

            options.DocumentFilter<AtmetOpenApiDocumentFilter>();
            options.OperationFilter<AtmetPortalHeadersOperationFilter>();

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
                Description = """
                    Integration API key (shared secret). Send on **every** `/api/v1/*` request.

                    **Header:** `X-Api-Key: <your-key>`

                    Keys are provisioned in server configuration (`ApiKeys:Keys`). Store keys in a secret manager in production; rotate by updating configuration and redeploying.

                    **Note:** The OpenAPI document applies this security scheme globally to versioned API operations. Portal routes still require portal context headers—see the main description.
                    """
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
