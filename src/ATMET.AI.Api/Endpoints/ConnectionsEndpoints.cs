using ATMET.AI.Core.Models.Responses;
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
            .WithTags("Connections");

        // IMPORTANT: Register literal "/default" BEFORE parameterized "/{connectionName}"
        // to avoid route shadowing
        connections.MapGet("/default", GetDefaultConnection)
            .WithName("GetDefaultConnection")
            .WithSummary("Get the default project connection")
            .WithDescription("Returns the default Azure AI resource connection configured for the project.")
            .Produces<ConnectionResponse>()
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        connections.MapGet("/", ListConnections)
            .WithName("ListConnections")
            .WithSummary("List all Azure resource connections")
            .WithDescription("Returns all Azure resource connections (OpenAI, Azure AI Search, etc.) linked to the project. Optionally filter by connection type.")
            .Produces<List<ConnectionResponse>>()
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        connections.MapGet("/{connectionName}", GetConnection)
            .WithName("GetConnection")
            .WithSummary("Get connection details by name")
            .WithDescription("Returns metadata for a specific connection. Credentials are not included.")
            .Produces<ConnectionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
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
