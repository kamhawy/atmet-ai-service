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
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "ATMET.AI.Service")
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .WriteTo.Console()
            .WriteTo.ApplicationInsights(
                configuration["ApplicationInsights:ConnectionString"],
                TelemetryConverter.Traces)
            .CreateLogger();

        host.UseSerilog();
        return host;
    }
}
