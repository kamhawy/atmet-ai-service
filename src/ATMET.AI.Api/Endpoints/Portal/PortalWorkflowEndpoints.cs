using ATMET.AI.Core.Models.Portal;
using ATMET.AI.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ATMET.AI.Api.Endpoints.Portal;

/// <summary>
/// Workflow progress for a portal case.
/// </summary>
public static class PortalWorkflowEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        var workflow = group.MapGroup("/portal/cases/{caseId}/workflow")
            .WithTags("Portal - Workflow");

        workflow.MapGet("/", GetWorkflowState)
            .WithName("GetPortalWorkflowState")
            .WithSummary("Get computed workflow state for a case")
            .WithDescription("""
                Returns **`WorkflowStateResponse`**: current step id, **progress percent**, totals, and **per-step status** (pending / active / completed with timestamps).

                **Business use:** progress bars and stepper UI; complements **`GET /portal/cases/{caseId}`** which embeds similar data on the detail payload.

                **Headers:** `X-Portal-User-Id` (required). **`404`** if unavailable.
                """)
            .Produces<WorkflowStateResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization("ApiReader");

        workflow.MapPost("/steps/{stepId}/complete", CompleteStep)
            .WithName("CompletePortalWorkflowStep")
            .WithSummary("Mark a workflow step as completed")
            .WithDescription("""
                Idempotent-style **step completion** for guided flows. Optional body: **`comment`**.

                **Business use:** citizen or agent-driven advancement when a step’s requirements are satisfied.

                **Headers:** `X-Portal-User-Id` (required).
                """)
            .Produces<WorkflowStateResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
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
