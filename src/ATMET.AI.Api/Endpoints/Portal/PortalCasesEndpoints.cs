using ATMET.AI.Core.Models.Portal;
using ATMET.AI.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ATMET.AI.Api.Endpoints.Portal;

/// <summary>
/// Citizen portal cases (applications) backed by Supabase.
/// </summary>
public static class PortalCasesEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        var cases = group.MapGroup("/portal/cases")
            .WithTags("Portal - Cases");

        cases.MapPost("/", CreateCase)
            .WithName("CreatePortalCase")
            .WithSummary("Create a new case (application)")
            .WithDescription("""
                Opens a **new case** for the authenticated portal user against a **service** and **entity**.

                **Request body (`CreateCaseRequest`):**
                - `serviceId` — target service UUID.
                - `entityId` — must align with tenant policy (typically same as `X-Portal-Entity-Id` when enforced by your BFF).
                - `submittedData` / `eligibilityResult` — optional JSON blobs captured during pre-check or wizard bootstrap.

                **Response:** `201 Created` with `PortalCaseResponse` including **`referenceNumber`** for citizen-facing tracking.

                **Headers:** `X-Portal-User-Id` (required).
                """)
            .Produces<PortalCaseResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization("ApiWriter");

        cases.MapGet("/", GetCases)
            .WithName("GetPortalCases")
            .WithSummary("List cases for the authenticated user")
            .WithDescription("""
                Returns **all cases** for **`X-Portal-User-Id`** scoped to **`X-Portal-Entity-Id`**, optionally filtered by **`status`** (query string, matches case status text in storage).

                **Business use:** “My applications” dashboard.

                **Headers:** `X-Portal-User-Id` and `X-Portal-Entity-Id` (required).
                """)
            .Produces<List<PortalCaseResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization("ApiReader");

        cases.MapGet("/{caseId}", GetCase)
            .WithName("GetPortalCase")
            .WithSummary("Get case detail with computed workflow state")
            .WithDescription("""
                Returns **`PortalCaseDetailResponse`**: case metadata, submitted JSON, eligibility snapshot, and **embedded `workflowState`** (current step, progress, step statuses).

                **Business use:** application detail screen and agent tool responses.

                **Headers:** `X-Portal-User-Id` (required). Returns **`404`** if the case does not exist or is not owned by the user.
                """)
            .Produces<PortalCaseDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization("ApiReader");

        cases.MapPatch("/{caseId}/status", UpdateCaseStatus)
            .WithName("UpdatePortalCaseStatus")
            .WithSummary("Update case status")
            .WithDescription("""
                Applies a **status transition** (for example workflow automation or officer-simulator flows). Body: **`status`** (required text) and optional **`comment`**.

                **Business use:** advance or correct pipeline state when integrating back-office tools; citizen clients typically use form submit / workflow endpoints instead.

                **Headers:** `X-Portal-User-Id` (required).
                """)
            .Produces<PortalCaseResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization("ApiWriter");
    }

    private static async Task<IResult> CreateCase(
        [FromBody] CreateCaseRequest request,
        [FromHeader(Name = "X-Portal-User-Id")] string userId,
        [FromServices] IPortalCaseService service,
        CancellationToken ct)
    {
        var result = await service.CreateCaseAsync(request, userId, ct);
        return Results.Created($"/api/v1/portal/cases/{result.Id}", result);
    }

    private static async Task<IResult> GetCases(
        [FromHeader(Name = "X-Portal-User-Id")] string userId,
        [FromHeader(Name = "X-Portal-Entity-Id")] string entityId,
        [FromQuery] string? status,
        [FromServices] IPortalCaseService service,
        CancellationToken ct)
    {
        var result = await service.GetCasesAsync(userId, entityId, status, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetCase(
        string caseId,
        [FromHeader(Name = "X-Portal-User-Id")] string userId,
        [FromServices] IPortalCaseService service,
        CancellationToken ct)
    {
        var result = await service.GetCaseAsync(caseId, userId, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> UpdateCaseStatus(
        string caseId,
        [FromBody] UpdateCaseStatusRequest request,
        [FromHeader(Name = "X-Portal-User-Id")] string userId,
        [FromServices] IPortalCaseService service,
        CancellationToken ct)
    {
        var result = await service.UpdateCaseStatusAsync(caseId, userId, request, ct);
        return Results.Ok(result);
    }
}
