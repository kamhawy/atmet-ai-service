using ATMET.AI.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ATMET.AI.Api.Endpoints.Portal;

public static class PortalServicesEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        var services = group.MapGroup("/portal/services")
            .WithTags("Portal - Services");

        services.MapGet("/", GetServices)
            .WithName("GetPortalServices")
            .WithSummary("List active services for an entity")
            .RequireAuthorization("ApiReader")
            .CacheOutput("PortalCatalog");

        services.MapGet("/{serviceId}", GetService)
            .WithName("GetPortalService")
            .WithSummary("Get service detail including form schema")
            .RequireAuthorization("ApiReader")
            .CacheOutput("PortalCatalog");

        services.MapGet("/{serviceId}/workflow", GetServiceWorkflow)
            .WithName("GetPortalServiceWorkflow")
            .WithSummary("Get workflow definition for a service")
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
