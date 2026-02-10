using Azure.AI.Projects;
using Azure.AI.Agents.Persistent;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ATMET.AI.Infrastructure.Configuration;

namespace ATMET.AI.Infrastructure.Clients
{
    // ====================================================================================
    // Azure AI Client Factory
    // ====================================================================================

    /// <summary>
    /// Factory for creating Azure AI clients with managed identity
    /// </summary>
    public class AzureAIClientFactory
    {
        private readonly AzureAIOptions _options;
        private readonly ILogger<AzureAIClientFactory> _logger;
        private readonly Lazy<AIProjectClient> _projectClient;
        private readonly Lazy<PersistentAgentsClient> _agentsClient;

        public AzureAIClientFactory(
            IOptions<AzureAIOptions> options,
            ILogger<AzureAIClientFactory> logger)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _projectClient = new Lazy<AIProjectClient>(CreateProjectClient);
            _agentsClient = new Lazy<PersistentAgentsClient>(CreateAgentsClient);
        }

        public AIProjectClient GetProjectClient() => _projectClient.Value;
        public PersistentAgentsClient GetAgentsClient() => _agentsClient.Value;

        private AIProjectClient CreateProjectClient()
        {
            try
            {
                var credential = CreateCredential();
                var endpoint = new Uri(_options.ProjectEndpoint);

                _logger.LogInformation(
                    "Creating AIProjectClient for endpoint: {Endpoint}",
                    _options.ProjectEndpoint);

                return new AIProjectClient(endpoint, credential);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create AIProjectClient");
                throw;
            }
        }

        private PersistentAgentsClient CreateAgentsClient()
        {
            try
            {
                var credential = CreateCredential();

                _logger.LogInformation(
                    "Creating PersistentAgentsClient for endpoint: {Endpoint}",
                    _options.ProjectEndpoint);

                return new PersistentAgentsClient(_options.ProjectEndpoint, credential);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create PersistentAgentsClient");
                throw;
            }
        }

        private TokenCredential CreateCredential()
        {
            if (!string.IsNullOrEmpty(_options.ManagedIdentityClientId))
            {
                _logger.LogInformation(
                    "Using User-Assigned Managed Identity: {ClientId}",
                    _options.ManagedIdentityClientId);

                return new ManagedIdentityCredential(_options.ManagedIdentityClientId);
            }

            _logger.LogInformation("Using Default Azure Credential (System-Assigned MI or local dev)");
            return new DefaultAzureCredential();
        }
    }

}
