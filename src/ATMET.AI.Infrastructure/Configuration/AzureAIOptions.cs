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
        public bool EnableTelemetry { get; set; } = true;
        public string DefaultModelDeployment { get; set; } = "gpt-4o";
        public int RequestTimeoutSeconds { get; set; } = 120;
        public int MaxRetryAttempts { get; set; } = 3;
    }
}
