namespace ATMET.AI.Infrastructure.Configuration;

/// <summary>
/// Configuration for Azure Cognitive Services Speech (token issuance for browser Speech SDK).
/// </summary>
public sealed class AzureSpeechOptions
{
    public const string SectionName = "AzureSpeech";

    /// <summary>Optional override for the issueToken URL. Leave empty to use the default URL derived from <see cref="Region"/>.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Azure region id (e.g. <c>westeurope</c>, <c>uksouth</c>).</summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>Speech resource subscription key (server-side only).</summary>
    public string Key { get; set; } = string.Empty;
}
