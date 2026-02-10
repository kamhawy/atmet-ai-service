using Microsoft.AspNetCore.Authentication;

namespace ATMET.AI.Api.Authentication;

/// <summary>
/// Options for the API Key authentication scheme.
/// </summary>
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string DefaultScheme = "ApiKey";

    /// <summary>
    /// HTTP header name used to pass the API key (e.g. X-Api-Key).
    /// </summary>
    public string HeaderName { get; set; } = "X-Api-Key";

    /// <summary>
    /// Set of valid API keys (populated from configuration).
    /// </summary>
    public IReadOnlySet<string> ValidKeys { get; set; } = new HashSet<string>(StringComparer.Ordinal);
}
