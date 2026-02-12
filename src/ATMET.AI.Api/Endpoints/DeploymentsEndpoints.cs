using ATMET.AI.Core.Models.Responses;
using ATMET.AI.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ATMET.AI.Api.Endpoints;

/// <summary>
/// Endpoints for managing AI model deployments
/// </summary>
public static class DeploymentsEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        var deployments = group.MapGroup("/deployments")
            .WithTags("Deployments");

        deployments.MapGet("/", ListDeployments)
            .WithName("ListDeployments")
            .WithSummary("List all AI model deployments")
            .WithDescription("Returns all model deployments (e.g., GPT-4, GPT-4o) available in the project. Optionally filter by publisher or type.")
            .Produces<List<DeploymentResponse>>()
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        deployments.MapGet("/{deploymentName}", GetDeployment)
            .WithName("GetDeployment")
            .WithSummary("Get deployment details by name")
            .WithDescription("Returns metadata for a specific model deployment including model name, publisher, capabilities, and SKU.")
            .Produces<DeploymentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> ListDeployments(
        [FromServices] IDeploymentService deploymentService,
        [FromQuery] string? modelPublisher,
        [FromQuery] string? modelType,
        CancellationToken cancellationToken)
    {
        var deployments = await deploymentService.ListDeploymentsAsync(
            modelPublisher, modelType, cancellationToken);
        return Results.Ok(deployments);
    }

    private static async Task<IResult> GetDeployment(
        string deploymentName,
        [FromServices] IDeploymentService deploymentService,
        CancellationToken cancellationToken)
    {
        var deployment = await deploymentService.GetDeploymentAsync(
            deploymentName, cancellationToken);
        return Results.Ok(deployment);
    }
}
