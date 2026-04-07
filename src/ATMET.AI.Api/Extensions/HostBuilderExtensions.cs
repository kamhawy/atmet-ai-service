using Serilog;
using Serilog.Sinks.ApplicationInsights;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;

namespace ATMET.AI.Api.Extensions;

/// <summary>
/// Extension methods for <see cref="IHostBuilder"/> to configure logging and hosting.
/// </summary>
public static class HostBuilderExtensions
{
    /// <summary>
    /// Configures Serilog with Application Insights, console (from configuration), and a rolling file sink.
    /// On Azure App Service, logs are written under <c>%HOME%/LogFiles/Application</c> so they appear in Log stream and Kudu.
    /// </summary>
    public static IHostBuilder UseApiSerilog(this IHostBuilder host, IConfiguration configuration)
    {
        var loggerConfig = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "ATMET.AI.Service")
            .Enrich.WithEnvironmentName()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithActivityDetails(true, true);

        var connectionString = configuration["ApplicationInsights:ConnectionString"];
        if (!string.IsNullOrEmpty(connectionString))
        {
            // TraceTelemetryConverter forwards W3C operation / parent span for correlation with App Insights requests.
            loggerConfig.WriteTo.ApplicationInsights(
                connectionString,
                new TraceTelemetryConverter(
                    includeOperationIdPropertyAsTelemetryProperty: true,
                    includeParentSpanIdPropertyAsTelemetryProperty: true,
                    includeOperationNamePropertyAsTelemetryProperty: true,
                    includeVersionPropertyAsTelemetryProperty: true,
                    ignorePropertyNameCase: false));
        }

        var filePath = ResolveRollingFilePath(configuration);
        var fileParent = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(fileParent))
            Directory.CreateDirectory(fileParent);

        var rollingInterval = ParseRollingInterval(configuration["Serilog:File:RollingInterval"]);
        var retainedFileCountLimit = configuration.GetValue<int?>("Serilog:File:RetainedFileCountLimit") ?? 31;
        var fileSizeLimitBytes = configuration.GetValue<long?>("Serilog:File:FileSizeLimitBytes");
        var rollOnFileSizeLimit = configuration.GetValue(
            "Serilog:File:RollOnFileSizeLimit",
            fileSizeLimitBytes is > 0);

        loggerConfig.WriteTo.File(
            path: filePath,
            rollingInterval: rollingInterval,
            fileSizeLimitBytes: fileSizeLimitBytes,
            rollOnFileSizeLimit: rollOnFileSizeLimit,
            retainedFileCountLimit: retainedFileCountLimit,
            shared: true,
            flushToDiskInterval: TimeSpan.FromSeconds(1));

        Log.Logger = loggerConfig.CreateLogger();

        host.UseSerilog(Log.Logger, dispose: true);
        return host;
    }

    /// <summary>
    /// Resolves the rolling log file path. When the configuration key <c>Serilog:File:Path</c> is empty,
    /// uses Azure App Service <c>HOME/LogFiles/Application</c> when <c>WEBSITE_SITE_NAME</c> is set; otherwise <c>logs/</c> under the app base directory.
    /// </summary>
    private static string ResolveRollingFilePath(IConfiguration configuration)
    {
        var configured = configuration["Serilog:File:Path"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var isAzureAppService = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));
        if (isAzureAppService)
        {
            var home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
                return Path.Combine(home, "LogFiles", "Application", "atmet-ai-.log");
        }

        return Path.Combine(AppContext.BaseDirectory, "logs", "atmet-ai-.log");
    }

    private static RollingInterval ParseRollingInterval(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return RollingInterval.Day;

        return Enum.TryParse<RollingInterval>(value, ignoreCase: true, out var interval)
            ? interval
            : RollingInterval.Day;
    }
}
