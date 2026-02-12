using ATMET.AI.Core.Models.Requests;
using ATMET.AI.Core.Models.Responses;
using ATMET.AI.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ATMET.AI.Api.Endpoints;

/// <summary>
/// Endpoints for managing Azure AI Search indexes
/// </summary>
public static class IndexesEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        var indexes = group.MapGroup("/indexes")
            .WithTags("Indexes");

        indexes.MapPost("/", CreateOrUpdateIndex)
            .WithName("CreateOrUpdateIndex")
            .WithSummary("Create or update a search index")
            .WithDescription("Creates or updates an Azure AI Search index definition. Requires name, version, connectionName, and indexName. Optional description, tags, and fieldMapping.")
            .RequireAuthorization("ApiWriter")
            .Produces<IndexResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        indexes.MapGet("/", ListIndexes)
            .WithName("ListIndexes")
            .WithSummary("List all indexes")
            .WithDescription("Returns the latest version of each index in the project.")
            .Produces<List<IndexResponse>>();

        indexes.MapGet("/{name}/versions", ListIndexVersions)
            .WithName("ListIndexVersions")
            .WithSummary("List all versions of an index")
            .WithDescription("Returns all versions of an index by name.")
            .Produces<List<IndexResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        indexes.MapGet("/{name}/versions/{version}", GetIndex)
            .WithName("GetIndex")
            .WithSummary("Get a specific index version")
            .WithDescription("Returns index metadata including connectionName, indexName, description, indexType, tags, and fieldMapping.")
            .Produces<IndexResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        indexes.MapDelete("/{name}/versions/{version}", DeleteIndex)
            .WithName("DeleteIndex")
            .WithSummary("Delete an index version")
            .WithDescription("Permanently removes an index version from the project.")
            .RequireAuthorization("ApiWriter")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    // ====================================================================
    // Handler Methods
    // ====================================================================

    private static async Task<IResult> CreateOrUpdateIndex(
        [FromBody] CreateIndexRequest request,
        [FromServices] IIndexService indexService,
        CancellationToken cancellationToken)
    {
        var index = await indexService.CreateOrUpdateIndexAsync(
            request.Name, request.Version, request.ConnectionName,
            request.IndexName, request.Description, cancellationToken);

        return Results.Created(
            $"/api/v1/indexes/{index.Name}/versions/{index.Version}", index);
    }

    private static async Task<IResult> ListIndexes(
        [FromServices] IIndexService indexService,
        CancellationToken cancellationToken)
    {
        var indexes = await indexService.ListIndexesAsync(cancellationToken);
        return Results.Ok(indexes);
    }

    private static async Task<IResult> ListIndexVersions(
        string name,
        [FromServices] IIndexService indexService,
        CancellationToken cancellationToken)
    {
        var versions = await indexService.ListIndexVersionsAsync(name, cancellationToken);
        return Results.Ok(versions);
    }

    private static async Task<IResult> GetIndex(
        string name,
        string version,
        [FromServices] IIndexService indexService,
        CancellationToken cancellationToken)
    {
        var index = await indexService.GetIndexAsync(name, version, cancellationToken);
        return Results.Ok(index);
    }

    private static async Task<IResult> DeleteIndex(
        string name,
        string version,
        [FromServices] IIndexService indexService,
        CancellationToken cancellationToken)
    {
        await indexService.DeleteIndexAsync(name, version, cancellationToken);
        return Results.NoContent();
    }
}
