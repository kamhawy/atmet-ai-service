using System.Diagnostics;

namespace ATMET.AI.Infrastructure.Services.PortalAiWorkflow;

/// <summary>
/// Distributed tracing source for portal Foundry workflow turns (entity-agnostic orchestration).
/// Exported via OpenTelemetry in <c>ATMET.AI.Api.Extensions.ObservabilityExtensions</c> (<c>AddSource</c> uses <see cref="Source"/> name).
/// </summary>
public static class PortalAiWorkflowTelemetry
{
    public static readonly ActivitySource Source = new("ATMET.AI.PortalWorkflow", "1.0.0");
}
