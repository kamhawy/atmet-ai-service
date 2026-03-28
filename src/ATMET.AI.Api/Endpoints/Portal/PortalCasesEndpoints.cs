using ATMET.AI.Core.Models.Portal;
using ATMET.AI.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ATMET.AI.Api.Endpoints.Portal;

public static class PortalCasesEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        var cases = group.MapGroup("/portal/cases")
            .WithTags("Portal - Cases");

        cases.MapPost("/", CreateCase)
            .WithName("CreatePortalCase")
            .WithSummary("Create a new case (application)")
            .RequireAuthorization("ApiWriter");

        cases.MapGet("/", GetCases)
            .WithName("GetPortalCases")
            .WithSummary("List cases for the authenticated user")
            .RequireAuthorization("ApiReader");

        cases.MapGet("/{caseId}", GetCase)
            .WithName("GetPortalCase")
            .WithSummary("Get case detail with computed workflow state")
            .RequireAuthorization("ApiReader");

        cases.MapPatch("/{caseId}/status", UpdateCaseStatus)
            .WithName("UpdatePortalCaseStatus")
            .WithSummary("Update case status")
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
