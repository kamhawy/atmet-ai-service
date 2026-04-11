using Microsoft.Extensions.Configuration;

namespace ATMET.AI.WorkflowSpike;

/// <summary>
/// Resolves Foundry workflow spike settings using the same keys as the API host (<c>AzureAI</c> section / <c>AzureAIOptions</c>),
/// including overrides via standard .NET env binding (<c>AzureAI__*</c>).
/// </summary>
internal static class SpikeConfiguration
{
    internal const string AzureAiSection = "AzureAI";

    /// <summary>Directory from which <c>appsettings*.json</c> was loaded, if any.</summary>
    internal static string? LastResolvedAppSettingsDirectory { get; private set; }

    /// <summary>
    /// Optional: directory that contains <c>appsettings.json</c> (typically <c>.../ATMET.AI.Api</c>).
    /// </summary>
    public const string AppSettingsDirectoryEnv = "ATMET_APPSETTINGS_DIR";

    /// <summary>
    /// Builds configuration: optional Api <c>appsettings*.json</c> (lowest), then environment variables (highest).
    /// Environment uses the same binding as the running host, e.g. <c>AzureAI__ProjectEndpoint</c>.
    /// </summary>
    public static IConfigurationRoot Build()
    {
        var envName =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";

        var builder = new ConfigurationBuilder();

        LastResolvedAppSettingsDirectory = ResolveAppSettingsDirectory();
        if (LastResolvedAppSettingsDirectory != null)
        {
            builder.SetBasePath(LastResolvedAppSettingsDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile($"appsettings.{envName}.json", optional: true, reloadOnChange: false);
        }

        builder.AddEnvironmentVariables();
        return builder.Build();
    }

    /// <summary>Foundry project URL — <c>AzureAI:ProjectEndpoint</c> in JSON, <c>AzureAI__ProjectEndpoint</c> in env.</summary>
    public static string GetProjectEndpoint(IConfiguration config)
    {
        var v = config[$"{AzureAiSection}:ProjectEndpoint"];
        if (string.IsNullOrWhiteSpace(v))
        {
            throw new InvalidOperationException(
                "Missing project endpoint. Set AzureAI:ProjectEndpoint in ATMET.AI.Api/appsettings.json " +
                "(or appsettings.{Environment}.json), or environment variable AzureAI__ProjectEndpoint. " +
                $"Optionally set {AppSettingsDirectoryEnv} to the Api project folder.");
        }

        return v;
    }

    public static string GetWorkflowAgentName(IConfiguration config)
    {
        var v = config[$"{AzureAiSection}:WorkflowAgentName"];
        if (string.IsNullOrWhiteSpace(v))
        {
            throw new InvalidOperationException(
                "Missing workflow agent name. Set AzureAI:WorkflowAgentName in ATMET.AI.Api/appsettings.json " +
                "(or appsettings.{Environment}.json), or environment variable AzureAI__WorkflowAgentName.");
        }

        return v;
    }

    public static string GetWorkflowAgentVersion(IConfiguration config)
    {
        var v = config[$"{AzureAiSection}:WorkflowAgentVersion"];
        if (string.IsNullOrWhiteSpace(v))
        {
            throw new InvalidOperationException(
                "Missing workflow agent version. Set AzureAI:WorkflowAgentVersion in ATMET.AI.Api/appsettings.json " +
                "(or appsettings.{Environment}.json), or environment variable AzureAI__WorkflowAgentVersion.");
        }

        return v;
    }

    private static string? ResolveAppSettingsDirectory()
    {
        var explicitDir = Environment.GetEnvironmentVariable(AppSettingsDirectoryEnv);
        if (!string.IsNullOrWhiteSpace(explicitDir))
        {
            var full = Path.GetFullPath(explicitDir.Trim());
            if (File.Exists(Path.Combine(full, "appsettings.json")))
                return full;
        }

        foreach (var root in EnumerationRoots())
        {
            var dir = root;
            for (var i = 0; i < 12 && dir != null; i++, dir = dir.Parent)
            {
                var api = Path.Combine(dir.FullName, "src", "ATMET.AI.Api", "appsettings.json");
                if (File.Exists(api))
                    return Path.GetDirectoryName(api)!;
            }
        }

        return null;
    }

    private static IEnumerable<DirectoryInfo> EnumerationRoots()
    {
        if (!string.IsNullOrEmpty(Environment.CurrentDirectory))
            yield return new DirectoryInfo(Environment.CurrentDirectory);

        yield return new DirectoryInfo(AppContext.BaseDirectory);
    }

}
