// ====================================================================================
// Azure AI Configuration Options
// ====================================================================================

namespace ATMET.AI.Infrastructure.Configuration
{
    /// <summary>
    /// Configuration options for Azure AI services
    /// </summary>
    public class AzureAIOptions
    {
        public const string SectionName = "AzureAI";

        public string ProjectEndpoint { get; set; } = string.Empty;
        public string? ManagedIdentityClientId { get; set; }
        public string? ApiKey { get; set; }
        public string? AzureOpenAIEndpoint { get; set; }
        public bool EnableTelemetry { get; set; } = true;
        public string DefaultModelDeployment { get; set; } = "gpt-4o";

        /// <summary>
        /// Persistent agent assistant id from Foundry / API (<c>asst_*</c>). Used for direct agent GET.
        /// </summary>
        public string PortalAgentId { get; set; } = "asst_RwQkCaZPXdtFCVzZTPUEVjSm";

        /// <summary>
        /// Display name of the portal agent in Foundry (e.g. tax-agent). Used for list lookup when resolving by name.
        /// </summary>
        public string PortalAgentName { get; set; } = "atmet-portal-assistant";

        /// <summary>
        /// Default instructions when the portal assistant agent is first created in the project.
        /// </summary>
        public string PortalAgentInstructions { get; set; } =
            "You are a government services portal assistant. Help citizens with their applications.";

        public int RequestTimeoutSeconds { get; set; } = 120;
        public int MaxRetryAttempts { get; set; } = 3;
    }
}
