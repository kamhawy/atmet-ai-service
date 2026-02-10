using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using ATMET.AI.Infrastructure.Clients;

namespace ATMET.AI.Infrastructure.HealthChecks
{
    // ====================================================================================
    // Custom Health Check for Azure AI Connectivity
    // ====================================================================================

    /// <summary>
    /// Health check that validates Azure AI Foundry connectivity by listing deployments
    /// </summary>
    public class AzureAIHealthCheck : IHealthCheck
    {
        private readonly AzureAIClientFactory _clientFactory;
        private readonly ILogger<AzureAIHealthCheck> _logger;

        public AzureAIHealthCheck(
            AzureAIClientFactory clientFactory,
            ILogger<AzureAIHealthCheck> logger)
        {
            _clientFactory = clientFactory;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var client = _clientFactory.GetProjectClient();
                // Attempt to list deployments as a connectivity check
                var deployments = client.Deployments.GetDeployments(cancellationToken: cancellationToken);
                foreach (var _ in deployments)
                {
                    // Successfully connected — one result is enough
                    break;
                }

                return HealthCheckResult.Healthy("Azure AI Foundry connection is healthy");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Azure AI Foundry health check failed");
                return HealthCheckResult.Unhealthy(
                    "Azure AI Foundry connection is unhealthy",
                    exception: ex);
            }
        }
    }

}
