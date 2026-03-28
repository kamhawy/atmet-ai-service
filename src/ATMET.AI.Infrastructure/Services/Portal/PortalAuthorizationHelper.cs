using ATMET.AI.Core.Exceptions;
using ATMET.AI.Infrastructure.Clients;
using System.Text.Json;

namespace ATMET.AI.Infrastructure.Services.Portal;

internal static class PortalAuthorizationHelper
{
    /// <summary>
    /// Verifies that the given user is the requester (owner) of the specified case.
    /// Throws NotFoundException if the case doesn't exist, ForbiddenException if the user doesn't own it.
    /// </summary>
    internal static async Task VerifyCaseOwnershipAsync(
        SupabaseRestClient db, string caseId, string userId, CancellationToken ct = default)
    {
        var caseRow = await db.GetByIdAsync<JsonElement>("cases", caseId,
            select: "requester_user_id", cancellationToken: ct);

        if (caseRow.ValueKind == JsonValueKind.Undefined)
            throw new NotFoundException($"Case {caseId} not found");

        var ownerId = caseRow.GetProp("requester_user_id");
        if (ownerId != userId)
            throw new ForbiddenException("You do not have access to this case");
    }
}
