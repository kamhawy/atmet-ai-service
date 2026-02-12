// ====================================================================================
// Service Registration Extensions
// ====================================================================================

namespace ATMET.AI.Infrastructure.Extensions;

using ATMET.AI.Core.Services;
using ATMET.AI.Infrastructure.Clients;
using ATMET.AI.Infrastructure.Configuration;
using ATMET.AI.Infrastructure.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAzureAIServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration
        services.Configure<AzureAIOptions>(
            configuration.GetSection(AzureAIOptions.SectionName));

        // Register client factory as singleton (clients are Lazy<T> internally)
        services.AddSingleton<AzureAIClientFactory>();

        // Register all service implementations
        services.AddScoped<IAgentService, Services.AgentService>();
        services.AddScoped<IDeploymentService, Services.DeploymentService>();
        services.AddScoped<IConnectionService, Services.ConnectionService>();
        services.AddScoped<IDatasetService, Services.DatasetService>();
        services.AddScoped<IIndexService, Services.IndexService>();
        services.AddScoped<IChatService, Services.ChatService>();

        return services;
    }

    public static IHealthChecksBuilder AddAzureAIHealthCheck(
        this IHealthChecksBuilder builder)
    {
        return builder.AddCheck<AzureAIHealthCheck>(
            "azure-ai-foundry",
            tags: new[] { "ready" },
            timeout: TimeSpan.FromSeconds(15));
    }
}
