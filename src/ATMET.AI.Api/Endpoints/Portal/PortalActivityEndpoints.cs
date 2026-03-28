using ATMET.AI.Core.Models.Portal;
using ATMET.AI.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ATMET.AI.Api.Endpoints.Portal;

/// <summary>
/// Case activity / audit timeline for the portal.
/// </summary>
public static class PortalActivityEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        var activity = group.MapGroup("/portal/cases/{caseId}/activity")
            .WithTags("Portal - Activity");

        activity.MapGet("/", GetActivity)
            .WithName("GetPortalActivity")
            .WithSummary("Get case audit log / activity timeline")
            .WithDescription("""
                Read-only **audit trail** for the case: action type, actor, status transitions, optional JSON **`actionPayload`**, timestamps.

                **Business use:** “Activity” tab in the citizen portal and support tooling integrations.

                **Headers:** `X-Portal-User-Id` (required).
                """)
            .Produces<List<ActivityEntryResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
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
