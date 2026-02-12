using Serilog;

namespace ATMET.AI.Api.Extensions;

/// <summary>
/// Extension methods for <see cref="IHostBuilder"/> to configure logging and hosting.
/// </summary>
public static class HostBuilderExtensions
{
    /// <summary>
    /// Configures Serilog with Application Insights and console sinks.
    /// </summary>
    public static IHostBuilder UseApiSerilog(this IHostBuilder host, IConfiguration configuration)
    {
        var loggerConfig = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "ATMET.AI.Service")
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .WriteTo.Console();

        var connectionString = configuration["ApplicationInsights:ConnectionString"];
        if (!string.IsNullOrEmpty(connectionString))
        {
            loggerConfig.WriteTo.ApplicationInsights(connectionString, TelemetryConverter.Traces);
        }

        Log.Logger = loggerConfig.CreateLogger();

        host.UseSerilog();
        return host;
    }
}
