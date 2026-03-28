using ATMET.AI.Core.Models.Portal;
using ATMET.AI.Core.Services;
using ATMET.AI.Infrastructure.Clients;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ATMET.AI.Infrastructure.Services.Portal;

public class PortalActivityService : IPortalActivityService
{
    private readonly SupabaseRestClient _db;
    private readonly ILogger<PortalActivityService> _logger;

    public PortalActivityService(SupabaseRestClient db, ILogger<PortalActivityService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<ActivityEntryResponse>> GetActivityAsync(string caseId, string userId, CancellationToken ct = default)
    {
        // Verify user owns the case
        var caseRow = await _db.GetByIdAsync<JsonElement>("cases", caseId,
            select: "requester_user_id", cancellationToken: ct);
        if (caseRow.ValueKind == JsonValueKind.Undefined) return [];
        if (caseRow.GetProperty("requester_user_id").GetString() != userId) return [];

        var rows = await _db.GetAsync<JsonElement>("case_audit_log",
            filters: [$"case_id=eq.{caseId}"],
            order: "created_at.asc",
            cancellationToken: ct);

        return rows.Select(r => new ActivityEntryResponse(
            Id: r.GetProperty("id").GetString()!,
            ActionType: r.GetProperty("action_type").GetString()!,
            ActorUserId: r.GetProp("actor_user_id"),
            ActorRole: r.GetProp("actor_role"),
            Comment: r.GetProp("comment"),
            PreviousStatus: r.GetProp("previous_status"),
            NewStatus: r.GetProp("new_status"),
            ActionPayload: r.GetJsonProp("action_payload"),
            CreatedAt: r.GetProperty("created_at").GetDateTimeOffset()
        )).ToList();
    }
}
