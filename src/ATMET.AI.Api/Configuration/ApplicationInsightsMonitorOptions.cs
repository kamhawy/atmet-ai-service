namespace ATMET.AI.Api.Configuration;

/// <summary>
/// Binds <c>ApplicationInsights</c> from configuration (same keys as the classic Application Insights SDK).
/// Used by <see cref="Extensions.ObservabilityExtensions"/> for OpenTelemetry + <see cref="Microsoft.ApplicationInsights.TelemetryClient"/>.
/// </summary>
public sealed class ApplicationInsightsMonitorOptions
{
    public const string SectionName = "ApplicationInsights";

    public string? ConnectionString { get; set; }

    /// <summary>When <c>true</c> (default), use a ratio-based parent sampler similar to classic adaptive sampling volume.</summary>
    public bool EnableAdaptiveSampling { get; set; } = true;

    /// <summary>When <c>true</c> (default), register full Azure Monitor distro (including outgoing HTTP dependencies). When <c>false</c>, traces only: ASP.NET Core + portal workflow <c>ActivitySource</c>, no HTTP client instrumentation.</summary>
    public bool EnableDependencyTracking { get; set; } = true;

    /// <summary>Classic SDK performance counters (Windows). OpenTelemetry uses runtime / ASP.NET metrics instead; this flag only affects whether we register <see cref="OpenTelemetry.Metrics.MeterProviderBuilder"/> in the trace-only path.</summary>
    public bool EnablePerformanceCounterCollectionModule { get; set; } = true;
}
