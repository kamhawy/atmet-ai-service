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
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

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

    public static IServiceCollection AddSupabaseServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration
        services.Configure<SupabaseOptions>(
            configuration.GetSection(SupabaseOptions.SectionName));

        // Register SupabaseRestClient as a typed HttpClient (singleton-safe via HttpClientFactory)
        services.AddHttpClient<SupabaseRestClient>()
            .ConfigureHttpClient((sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<SupabaseOptions>>().Value;
                client.BaseAddress = new Uri(opts.Url.TrimEnd('/'));
                client.DefaultRequestHeaders.Add("apikey", opts.AnonKey);
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", opts.ServiceRoleKey);
            })
            .AddStandardResilienceHandler();

        // Register portal service implementations
        services.AddScoped<IPortalCatalogService, Services.Portal.PortalCatalogService>();
        services.AddScoped<IPortalCaseService, Services.Portal.PortalCaseService>();
        services.AddScoped<IPortalConversationService, Services.Portal.PortalConversationService>();
        services.AddScoped<IPortalDocumentService, Services.Portal.PortalDocumentService>();
        services.AddScoped<IPortalFormService, Services.Portal.PortalFormService>();
        services.AddScoped<IPortalWorkflowService, Services.Portal.PortalWorkflowService>();
        services.AddScoped<IPortalActivityService, Services.Portal.PortalActivityService>();
        services.AddScoped<IFoundryAgentReadService, Services.Foundry.FoundryAgentReadService>();
        services.AddScoped<IPortalAiWorkflowService, Services.PortalAiWorkflow.PortalAiWorkflowService>();

        // Portal AI agent service (orchestrates chat → tool calls → portal services)
        services.AddScoped<IPortalAgentService, Services.Portal.PortalAgentService>();

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

    public static IHealthChecksBuilder AddSupabaseHealthCheck(
        this IHealthChecksBuilder builder)
    {
        return builder.AddCheck<SupabaseHealthCheck>(
            "supabase",
            tags: new[] { "ready" },
            timeout: TimeSpan.FromSeconds(10));
    }
}
