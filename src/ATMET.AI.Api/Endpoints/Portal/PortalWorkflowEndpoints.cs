using ATMET.AI.Core.Models.Portal;
using ATMET.AI.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ATMET.AI.Api.Endpoints.Portal;

public static class PortalWorkflowEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        var workflow = group.MapGroup("/portal/cases/{caseId}/workflow")
            .WithTags("Portal - Workflow");

        workflow.MapGet("/", GetWorkflowState)
            .WithName("GetPortalWorkflowState")
            .WithSummary("Get computed workflow state for a case")
            .RequireAuthorization("ApiReader");

        workflow.MapPost("/steps/{stepId}/complete", CompleteStep)
            .WithName("CompletePortalWorkflowStep")
            .WithSummary("Mark a workflow step as completed")
            .RequireAuthorization("ApiWriter");
    }

    private static async Task<IResult> GetWorkflowState(
        string caseId,
        [FromHeader(Name = "X-Portal-User-Id")] string userId,
        [FromServices] IPortalWorkflowService service,
        CancellationToken ct)
    {
        var result = await service.GetWorkflowStateAsync(caseId, userId, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> CompleteStep(
        string caseId,
        string stepId,
        [FromBody] CompleteStepRequest? request,
        [FromHeader(Name = "X-Portal-User-Id")] string userId,
        [FromServices] IPortalWorkflowService service,
        CancellationToken ct)
    {
        var result = await service.CompleteStepAsync(caseId, stepId, userId, request, ct);
        return Results.Ok(result);
    }
}
