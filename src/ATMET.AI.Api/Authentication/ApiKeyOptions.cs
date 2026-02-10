namespace ATMET.AI.Api.Authentication;

/// <summary>
/// Options for API Key authentication.
/// </summary>
public class ApiKeyOptions
{
    public const string SectionName = "ApiKeys";

    /// <summary>
    /// HTTP header name used to pass the API key (e.g. X-Api-Key).
    /// </summary>
    public string HeaderName { get; set; } = "X-Api-Key";

    /// <summary>
    /// Valid API keys. Any key in this list is accepted for ApiReader and ApiWriter.
    /// </summary>
    public IReadOnlyList<string> Keys { get; set; } = [];
}
