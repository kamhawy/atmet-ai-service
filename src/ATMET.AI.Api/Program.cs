using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using ATMET.AI.Api.Endpoints;
using ATMET.AI.Api.Middleware;
using ATMET.AI.Infrastructure.Configuration;
using ATMET.AI.Infrastructure.Extensions;
using FluentValidation;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Identity.Web;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ====================================================================
// Configure Serilog
// ====================================================================
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "ATMET.AI.Service")
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console()
    .WriteTo.ApplicationInsights(
        builder.Configuration["ApplicationInsights:ConnectionString"],
        TelemetryConverter.Traces)
    .CreateLogger();

builder.Host.UseSerilog();

// ====================================================================
// Add Services to Container
// ====================================================================

// Azure AI Clients and Services (includes AzureAIOptions configuration)
builder.Services.AddAzureAIServices(builder.Configuration);

// Authentication & Authorization
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ApiReader", policy =>
        policy.RequireAuthenticatedUser());
    options.AddPolicy("ApiWriter", policy =>
        policy.RequireAuthenticatedUser()
               .RequireClaim("roles", "AI.Admin", "AI.Writer"));
});

// CORS Configuration for SPA
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSPA", policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("fixed", limiterOptions =>
    {
        limiterOptions.PermitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 100);
        limiterOptions.Window = TimeSpan.Parse(
            builder.Configuration.GetValue("RateLimiting:Window", "00:01:00")!);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = builder.Configuration.GetValue("RateLimiting:QueueLimit", 10);
    });

    // Stricter rate limit for write operations
    options.AddFixedWindowLimiter("writes", limiterOptions =>
    {
        limiterOptions.PermitLimit = 30;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 5;
    });
});

builder.Services.AddMemoryCache();

// HTTP Resilience
builder.Services.AddHttpClient("AzureAI")
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    });

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy())
    .AddAzureAIHealthCheck();

// API Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
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

    options.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    });

    options.AddSecurityRequirement(new()
    {
        {
            new()
            {
                Reference = new()
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// JSON Configuration
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Problem Details
builder.Services.AddProblemDetails();

// Application Insights
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
});

// FluentValidation — scan Infrastructure assembly for validators
builder.Services.AddValidatorsFromAssemblyContaining<AzureAIOptions>();

// ====================================================================
// Build Application
// ====================================================================
var app = builder.Build();

// ====================================================================
// Configure Middleware Pipeline
// ====================================================================

// Global Exception Handler
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Request Logging
app.UseMiddleware<RequestLoggingMiddleware>();

// Development Tools
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "ATMET AI Service API v1");
        options.RoutePrefix = string.Empty;
    });
}

// Security Headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'");
    await next();
});

app.UseHttpsRedirection();
app.UseCors("AllowSPA");

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

// Health Check Endpoints
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

// ====================================================================
// Map API Endpoints
// ====================================================================

var apiGroup = app.MapGroup("/api/v1")
    .RequireAuthorization("ApiReader")
    .RequireRateLimiting("fixed")
    .WithOpenApi();

// Only map endpoints for services that have real implementations
AgentsEndpoints.MapEndpoints(apiGroup);
DeploymentsEndpoints.MapEndpoints(apiGroup);
ConnectionsEndpoints.MapEndpoints(apiGroup);
DatasetsEndpoints.MapEndpoints(apiGroup);
IndexesEndpoints.MapEndpoints(apiGroup);
ChatEndpoints.MapEndpoints(apiGroup);

// Root endpoint (unauthenticated)
app.MapGet("/", () => Results.Ok(new
{
    Service = "ATMET AI Service",
    Version = "1.0.0",
    Status = "Running",
    Documentation = "/swagger",
    Health = "/health"
})).WithTags("Info");

// ====================================================================
// Run Application
// ====================================================================

try
{
    Log.Information("Starting ATMET AI Service");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
