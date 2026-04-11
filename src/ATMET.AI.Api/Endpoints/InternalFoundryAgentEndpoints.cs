using ATMET.AI.Api.Internal;
using ATMET.AI.Core.Models.Foundry;
using ATMET.AI.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ATMET.AI.Api.Endpoints;

/// <summary>
/// Internal HTTP surface for **Azure AI Foundry agent tools** (same API key as <c>/api/v1</c>; entity isolation via header).
/// </summary>
public static class InternalFoundryAgentEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        var foundry = group.MapGroup("/internal/foundry")
            .WithTags("Internal - Foundry tools");

        foundry.MapGet("/cases/{caseId}", GetCaseById)
            .WithName("FoundryInternal_GetCaseById")
            .WithSummary("Get case detail for a Foundry tool (by case id)")
            .WithDescription("""
                Returns **`CaseDetailForAgent`**: case metadata, submitted JSON, workflow snapshot, and **conversations**
                linked to the case (including Foundry session fields such as **`lastResponseId`**).

                **Headers:** **`X-Portal-Entity-Id`** (required) — must match the case's **`entity_id`**. Same keying convention as portal routes.

                **Auth:** `X-Api-Key` with **`ApiReader`** policy.

                **404** when the case does not exist or is not in the given entity.
                """)
            .Produces<CaseDetailForAgent>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization("ApiReader");

        foundry.MapGet("/cases/by-reference/{referenceNumber}", GetCaseByReference)
            .WithName("FoundryInternal_GetCaseByReference")
            .WithSummary("Get case detail for a Foundry tool (by reference number)")
            .WithDescription("""
                Same payload as **`GET .../cases/{caseId}`**, keyed by **`referenceNumber`** (citizen-facing reference).

                **Headers:** **`X-Portal-Entity-Id`** (required).
                """)
            .Produces<CaseDetailForAgent>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization("ApiReader");

        foundry.MapGet("/services/{serviceId}", GetService)
            .WithName("FoundryInternal_GetService")
            .WithSummary("Get service definition + workflow binding for a Foundry tool")
            .WithDescription("""
                Returns **`ServiceDetailForAgent`**: bilingual labels, **`formSchema`**, documents, intents, and **`workflow`**
                metadata when a binding exists.

                **Headers:** **`X-Portal-Entity-Id`** (required) — must match the service's **`entity_id`**.

                **404** when the service does not exist or belongs to another entity.
                """)
            .Produces<ServiceDetailForAgent>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization("ApiReader");
    }

    private static async Task<IResult> GetCaseById(
        string caseId,
        [FromHeader(Name = "X-Portal-Entity-Id")] string? entityId,
        [FromServices] IFoundryAgentReadService service,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(caseId))
            return Results.BadRequest(new { error = "caseId is required." });

        var tenant = RequireEntityId(entityId);
        if (tenant.Error != null)
            return tenant.Error;

        var result = await service.GetCaseForAgentAsync(caseId.Trim(), tenant.Value, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> GetCaseByReference(
        string referenceNumber,
        [FromHeader(Name = "X-Portal-Entity-Id")] string? entityId,
        [FromServices] IFoundryAgentReadService service,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(referenceNumber))
            return Results.BadRequest(new { error = "referenceNumber is required." });

        var tenant = RequireEntityId(entityId);
        if (tenant.Error != null)
            return tenant.Error;

        var result = await service.GetCaseByReferenceForAgentAsync(referenceNumber.Trim(), tenant.Value, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> GetService(
        string serviceId,
        [FromHeader(Name = "X-Portal-Entity-Id")] string? entityId,
        [FromServices] IFoundryAgentReadService service,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(serviceId))
            return Results.BadRequest(new { error = "serviceId is required." });

        var tenant = RequireEntityId(entityId);
        if (tenant.Error != null)
            return tenant.Error;

        var result = await service.GetServiceForAgentAsync(serviceId.Trim(), tenant.Value, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static (string Value, IResult? Error) RequireEntityId(string? entityId) =>
        FoundryToolTenantHeader.TryGetCanonicalEntityId(entityId, out var canonical)
            ? (canonical!, null)
            : (string.Empty, Results.BadRequest(new { error = "X-Portal-Entity-Id header is required." }));
}
