using ATMET.AI.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ATMET.AI.Api.Endpoints.Portal;

public static class PortalActivityEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        var activity = group.MapGroup("/portal/cases/{caseId}/activity")
            .WithTags("Portal - Activity");

        activity.MapGet("/", GetActivity)
            .WithName("GetPortalActivity")
            .WithSummary("Get case audit log / activity timeline")
            .RequireAuthorization("ApiReader");
    }

    private static async Task<IResult> GetActivity(
        string caseId,
        [FromHeader(Name = "X-Portal-User-Id")] string userId,
        [FromServices] IPortalActivityService service,
        CancellationToken ct)
    {
        var result = await service.GetActivityAsync(caseId, userId, ct);
        return Results.Ok(result);
    }
}
