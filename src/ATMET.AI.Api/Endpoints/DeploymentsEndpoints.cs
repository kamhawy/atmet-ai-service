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
            .WithDescription("""
                Enumerates **model deployments** in the connected Foundry project—use this to discover valid **`model`** strings for chat and agents.

                **Query filters:** optional **`modelPublisher`** and **`modelType`** narrow results when the backing API supports them.

                **Business use:** integration tests, deployment pickers in admin tools, capacity verification.
                """)
            .Produces<List<DeploymentResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        deployments.MapGet("/{deploymentName}", GetDeployment)
            .WithName("GetDeployment")
            .WithSummary("Get deployment details by name")
            .WithDescription("""
                Fetches **SKU, capabilities, publisher, status** for a single deployment by its resource name.

                **Path:** `deploymentName` is the deployment identifier as shown in Azure AI Foundry / Azure OpenAI (not always identical to the base model name).
                """)
            .Produces<DeploymentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
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
