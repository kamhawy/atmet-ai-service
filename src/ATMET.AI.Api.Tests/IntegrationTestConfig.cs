using System.Text.Json;

namespace ATMET.AI.Api.Tests;

/// <summary>
/// Reads <c>appsettings.Integration.json</c> copied next to the test assembly (must stay in sync with
/// <see cref="AtmetApiFactory"/> configuration merge order).
/// </summary>
internal static class IntegrationTestConfig
{
    private static readonly Lazy<string> ApiKeyLazy = new(LoadApiKey);

    public static string ApiKey => ApiKeyLazy.Value;

    private static string LoadApiKey()
    {
        var dir = Path.GetDirectoryName(typeof(IntegrationTestConfig).Assembly.Location)
                  ?? throw new InvalidOperationException("Cannot resolve test assembly directory.");
        var path = Path.Combine(dir, "appsettings.Integration.json");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"Missing {path}. Ensure appsettings.Integration.json is included with CopyToOutputDirectory.");
        }

        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream);
        var keys = doc.RootElement.GetProperty("ApiKeys").GetProperty("Keys");
        if (keys.GetArrayLength() == 0)
            throw new InvalidOperationException("appsettings.Integration.json must define at least one ApiKeys:Keys entry.");

        return keys[0].GetString() ?? throw new InvalidOperationException("ApiKeys:Keys[0] must be a non-null string.");
    }
}
