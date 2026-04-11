using ATMET.AI.Api.Configuration;
using ATMET.AI.Infrastructure.Services.PortalAiWorkflow;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ATMET.AI.Api.Extensions;

/// <summary>
/// Azure Monitor via OpenTelemetry (traces + metrics) and a lightweight <see cref="TelemetryClient"/> for
/// explicit exception tracking. Honors <see cref="ApplicationInsightsMonitorOptions"/> from the
/// <c>ApplicationInsights</c> configuration section. Custom workflow spans use <see cref="PortalAiWorkflowTelemetry.Source"/>.
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>
    /// Registers OpenTelemetry export to Azure Monitor and <see cref="TelemetryClient"/> when a connection string is configured.
    /// Skipped in <c>Testing</c> so integration tests do not emit telemetry to Azure.
    /// </summary>
    public static IServiceCollection AddApiAzureMonitorObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var aspNetEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? string.Empty;
        if (aspNetEnv.Equals("Testing", StringComparison.OrdinalIgnoreCase))
            return services;

        var ai = configuration.GetSection(ApplicationInsightsMonitorOptions.SectionName)
            .Get<ApplicationInsightsMonitorOptions>() ?? new ApplicationInsightsMonitorOptions();

        if (string.IsNullOrWhiteSpace(ai.ConnectionString))
            return services;

        services.Configure<ApplicationInsightsMonitorOptions>(
            configuration.GetSection(ApplicationInsightsMonitorOptions.SectionName));

        var sampler = BuildSampler(ai.EnableAdaptiveSampling);

        if (ai.EnableDependencyTracking)
        {
            services.AddOpenTelemetry()
                .ConfigureResource(rb => rb.AddService("ATMET.AI.Api", serviceVersion: "1.0.0"))
                .WithTracing(tracing =>
                {
                    tracing.SetSampler(sampler);
                    tracing.AddSource(PortalAiWorkflowTelemetry.Source.Name);
                })
                .UseAzureMonitor(options => options.ConnectionString = ai.ConnectionString);
        }
        else
        {
            // Trace-only: incoming ASP.NET + custom ActivitySource; no outgoing HTTP dependency spans.
            services.AddOpenTelemetry()
                .ConfigureResource(rb => rb.AddService("ATMET.AI.Api", serviceVersion: "1.0.0"))
                .WithTracing(tracing =>
                    tracing
                        .SetSampler(sampler)
                        .AddSource(PortalAiWorkflowTelemetry.Source.Name)
                        .AddAspNetCoreInstrumentation()
                        .AddAzureMonitorTraceExporter(o => o.ConnectionString = ai.ConnectionString))
                .WithMetrics(metrics =>
                {
                    if (ai.EnablePerformanceCounterCollectionModule)
                    {
                        metrics
                            .AddAspNetCoreInstrumentation()
                            .AddAzureMonitorMetricExporter(o => o.ConnectionString = ai.ConnectionString);
                    }
                });
        }

        services.AddSingleton(_ =>
        {
            var telemetryConfiguration = new TelemetryConfiguration { ConnectionString = ai.ConnectionString };
            return new TelemetryClient(telemetryConfiguration);
        });

        return services;
    }

    private static Sampler BuildSampler(bool enableAdaptiveSampling) =>
        enableAdaptiveSampling
            ? new ParentBasedSampler(new TraceIdRatioBasedSampler(0.25f))
            : new AlwaysOnSampler();
}
