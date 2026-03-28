using ATMET.AI.Infrastructure.Clients;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace ATMET.AI.Infrastructure.HealthChecks;

/// <summary>
/// Health check that validates Supabase PostgREST connectivity by querying the entities table.
/// </summary>
public class SupabaseHealthCheck : IHealthCheck
{
    private readonly SupabaseRestClient _client;
    private readonly ILogger<SupabaseHealthCheck> _logger;

    public SupabaseHealthCheck(
        SupabaseRestClient client,
        ILogger<SupabaseHealthCheck> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Query entities table with limit 1 as a connectivity check
            var result = await _client.GetAsync<object>(
                "entities",
                select: "id",
                limit: 1,
                cancellationToken: cancellationToken);

            return HealthCheckResult.Healthy("Supabase connection is healthy");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Supabase health check failed");
            return HealthCheckResult.Unhealthy(
                "Supabase connection is unhealthy",
                exception: ex);
        }
    }
}
