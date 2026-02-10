using ATMET.AI.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ATMET.AI.Api.Endpoints;

/// <summary>
/// Endpoints for managing Azure resource connections
/// </summary>
public static class ConnectionsEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        var connections = group.MapGroup("/connections")
            .WithTags("Connections")
            .WithOpenApi();

        // IMPORTANT: Register literal "/default" BEFORE parameterized "/{connectionName}"
        // to avoid route shadowing
        connections.MapGet("/default", GetDefaultConnection)
            .WithName("GetDefaultConnection")
            .WithSummary("Get the default project connection");

        connections.MapGet("/", ListConnections)
            .WithName("ListConnections")
            .WithSummary("List all Azure resource connections");

        connections.MapGet("/{connectionName}", GetConnection)
            .WithName("GetConnection")
            .WithSummary("Get connection details by name");
    }

    private static async Task<IResult> ListConnections(
        [FromServices] IConnectionService connectionService,
        [FromQuery] string? connectionType,
        CancellationToken cancellationToken = default)
    {
        var connections = await connectionService.ListConnectionsAsync(
            connectionType, cancellationToken);
        return Results.Ok(connections);
    }

    private static async Task<IResult> GetConnection(
        string connectionName,
        [FromServices] IConnectionService connectionService,
        CancellationToken cancellationToken = default)
    {
        var connection = await connectionService.GetConnectionAsync(
            connectionName, cancellationToken);
        return Results.Ok(connection);
    }

    private static async Task<IResult> GetDefaultConnection(
        [FromServices] IConnectionService connectionService,
        CancellationToken cancellationToken = default)
    {
        var connection = await connectionService.GetDefaultConnectionAsync(cancellationToken);
        return Results.Ok(connection);
    }
}
