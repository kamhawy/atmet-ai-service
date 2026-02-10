using ATMET.AI.Core.Models.Responses;
using ATMET.AI.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ATMET.AI.Api.Endpoints;

/// <summary>
/// Endpoints for managing datasets in Azure AI Foundry
/// </summary>
public static class DatasetsEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        var datasets = group.MapGroup("/datasets")
            .WithTags("Datasets")
            .WithOpenApi();

        datasets.MapPost("/upload/file", UploadFile)
            .WithName("UploadDatasetFile")
            .WithSummary("Upload a single file to create a dataset version")
            .DisableAntiforgery()
            .RequireAuthorization("ApiWriter")
            .Produces<DatasetResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        datasets.MapPost("/upload/folder", UploadFolder)
            .WithName("UploadDatasetFolder")
            .WithSummary("Upload multiple files to create a folder dataset version")
            .DisableAntiforgery()
            .RequireAuthorization("ApiWriter")
            .Produces<DatasetResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        datasets.MapGet("/", ListDatasets)
            .WithName("ListDatasets")
            .WithSummary("List latest versions of all datasets")
            .Produces<List<DatasetResponse>>();

        datasets.MapGet("/{name}/versions", ListDatasetVersions)
            .WithName("ListDatasetVersions")
            .WithSummary("List all versions of a dataset")
            .Produces<List<DatasetResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        datasets.MapGet("/{name}/versions/{version}", GetDataset)
            .WithName("GetDataset")
            .WithSummary("Get a specific dataset version")
            .Produces<DatasetResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        datasets.MapGet("/{name}/versions/{version}/credentials", GetDatasetCredentials)
            .WithName("GetDatasetCredentials")
            .WithSummary("Get SAS credentials for accessing dataset storage")
            .RequireAuthorization("ApiWriter")
            .Produces<DatasetCredentialsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        datasets.MapDelete("/{name}/versions/{version}", DeleteDataset)
            .WithName("DeleteDataset")
            .WithSummary("Delete a dataset version")
            .RequireAuthorization("ApiWriter")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    // ====================================================================
    // Handler Methods
    // ====================================================================

    private static async Task<IResult> UploadFile(
        [FromForm] string name,
        [FromForm] string version,
        [FromForm] string connectionName,
        IFormFile file,
        [FromServices] IDatasetService datasetService,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["file"] = ["A non-empty file is required"]
                });

        await using var stream = file.OpenReadStream();
        var dataset = await datasetService.UploadFileAsync(
            name, version, stream, file.FileName, connectionName, cancellationToken);

        return Results.Created(
            $"/api/v1/datasets/{dataset.Name}/versions/{dataset.Version}", dataset);
    }

    private static async Task<IResult> UploadFolder(
        [FromForm] string name,
        [FromForm] string version,
        [FromForm] string connectionName,
        [FromForm] string? filePattern,
        IFormFileCollection files,
        [FromServices] IDatasetService datasetService,
        CancellationToken cancellationToken)
    {
        if (files == null || files.Count == 0)
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["files"] = ["At least one file is required"]
                });

        // Convert IFormFileCollection to (Stream, FileName) tuples
        // Note: streams are kept open until the service finishes processing
        var fileEntries = new List<(Stream Stream, string FileName)>();
        var disposables = new List<Stream>();

        try
        {
            foreach (var file in files)
            {
                var stream = file.OpenReadStream();
                disposables.Add(stream);
                fileEntries.Add((stream, file.FileName));
            }

            var dataset = await datasetService.UploadFolderAsync(
                name, version, fileEntries, connectionName, filePattern, cancellationToken);

            return Results.Created(
                $"/api/v1/datasets/{dataset.Name}/versions/{dataset.Version}", dataset);
        }
        finally
        {
            foreach (var stream in disposables)
            {
                await stream.DisposeAsync();
            }
        }
    }

    private static async Task<IResult> ListDatasets(
        [FromServices] IDatasetService datasetService,
        CancellationToken cancellationToken)
    {
        var datasets = await datasetService.ListDatasetsAsync(cancellationToken);
        return Results.Ok(datasets);
    }

    private static async Task<IResult> ListDatasetVersions(
        string name,
        [FromServices] IDatasetService datasetService,
        CancellationToken cancellationToken)
    {
        var versions = await datasetService.ListDatasetVersionsAsync(name, cancellationToken);
        return Results.Ok(versions);
    }

    private static async Task<IResult> GetDataset(
        string name,
        string version,
        [FromServices] IDatasetService datasetService,
        CancellationToken cancellationToken)
    {
        var dataset = await datasetService.GetDatasetAsync(name, version, cancellationToken);
        return Results.Ok(dataset);
    }

    private static async Task<IResult> GetDatasetCredentials(
        string name,
        string version,
        [FromServices] IDatasetService datasetService,
        CancellationToken cancellationToken)
    {
        var credentials = await datasetService.GetCredentialsAsync(name, version, cancellationToken);
        return Results.Ok(credentials);
    }

    private static async Task<IResult> DeleteDataset(
        string name,
        string version,
        [FromServices] IDatasetService datasetService,
        CancellationToken cancellationToken)
    {
        await datasetService.DeleteDatasetAsync(name, version, cancellationToken);
        return Results.NoContent();
    }
}
