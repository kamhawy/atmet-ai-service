using ATMET.AI.Core.Models.Portal;
using ATMET.AI.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ATMET.AI.Api.Endpoints.Portal;

/// <summary>
/// Citizen portal service catalog (Supabase-backed, entity-scoped).
/// </summary>
public static class PortalServicesEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        var services = group.MapGroup("/portal/services")
            .WithTags("Portal - Services");

        services.MapGet("/", GetServices)
            .WithName("GetPortalServices")
            .WithSummary("List active services for an entity")
            .WithDescription("""
                Returns **published, active services** for the given **`X-Portal-Entity-Id`** (tenant). Each item includes bilingual names/descriptions, SLA hints, and category for building a service catalog UI.

                **Business use:** first step in MUBASHIR when a citizen selects which government service to apply for.

                **Caching:** responses may be served from output cache (short TTL).

                **Headers:** `X-Portal-Entity-Id` (required). No `X-Portal-User-Id` required for this list.
                """)
            .Produces<List<PortalServiceResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization("ApiReader")
            .CacheOutput("PortalCatalog");

        services.MapGet("/{serviceId}", GetService)
            .WithName("GetPortalService")
            .WithSummary("Get service detail including form schema")
            .WithDescription("""
                Returns a **single service** with **`formSchema`**, **`requiredDocuments`**, and **`intents`** JSON for dynamic UI generation and AI routing.

                **Business use:** hydrate the application wizard after the user picks a service.

                **Note:** this route does **not** require portal identity headers—only the public `serviceId` path parameter (UUID).
                """)
            .Produces<PortalServiceDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization("ApiReader")
            .CacheOutput("PortalCatalog");

        services.MapGet("/{serviceId}/workflow", GetServiceWorkflow)
            .WithName("GetPortalServiceWorkflow")
            .WithSummary("Get workflow definition for a service")
            .WithDescription("""
                Returns the **workflow version** bound to the service: identifiers, version number, and opaque **`config`** JSON describing steps and transitions.

                **Business use:** drive progress UI and step completion alongside case APIs.

                **Note:** no portal headers required—only `serviceId`.
                """)
            .Produces<PortalServiceWorkflowResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization("ApiReader")
            .CacheOutput("PortalCatalog");
    }

    private static async Task<IResult> GetServices(
        [FromHeader(Name = "X-Portal-Entity-Id")] string entityId,
        [FromServices] IPortalCatalogService service,
        CancellationToken ct)
    {
        var result = await service.GetServicesAsync(entityId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetService(
        string serviceId,
        [FromServices] IPortalCatalogService service,
        CancellationToken ct)
    {
        var result = await service.GetServiceAsync(serviceId, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> GetServiceWorkflow(
        string serviceId,
        [FromServices] IPortalCatalogService service,
        CancellationToken ct)
    {
        var result = await service.GetServiceWorkflowAsync(serviceId, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }
}
