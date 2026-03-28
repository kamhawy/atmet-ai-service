using ATMET.AI.Core.Models.Portal;
using ATMET.AI.Core.Services;
using ATMET.AI.Infrastructure.Clients;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ATMET.AI.Infrastructure.Services.Portal;

public class PortalCaseService : IPortalCaseService
{
    private readonly SupabaseRestClient _db;
    private readonly IPortalWorkflowService _workflowService;
    private readonly ILogger<PortalCaseService> _logger;

    public PortalCaseService(
        SupabaseRestClient db,
        IPortalWorkflowService workflowService,
        ILogger<PortalCaseService> logger)
    {
        _db = db;
        _workflowService = workflowService;
        _logger = logger;
    }

    public async Task<PortalCaseResponse> CreateCaseAsync(CreateCaseRequest request, string userId, CancellationToken ct = default)
    {
        // Look up service workflow binding to get workflow_version_id
        string? workflowVersionId = null;
        var bindings = await _db.GetAsync<JsonElement>("service_workflow_binding",
            select: "workflow_version_id,workflow_id",
            filters: [$"service_id=eq.{request.ServiceId}"],
            limit: 1,
            cancellationToken: ct);
        if (bindings.Count > 0)
            workflowVersionId = bindings[0].GetProp("workflow_version_id");

        // If no version from binding, try active version
        if (workflowVersionId == null && bindings.Count > 0)
        {
            var workflowId = bindings[0].GetProp("workflow_id");
            if (workflowId != null)
            {
                var versions = await _db.GetAsync<JsonElement>("workflow_versions",
                    select: "id",
                    filters: [$"workflow_id=eq.{workflowId}", "is_active=eq.true"],
                    limit: 1,
                    cancellationToken: ct);
                if (versions.Count > 0)
                    workflowVersionId = versions[0].GetProperty("id").GetString();
            }
        }

        var caseData = new Dictionary<string, object?>
        {
            ["entity_id"] = request.EntityId,
            ["service_id"] = request.ServiceId,
            ["requester_user_id"] = userId,
            ["status"] = "draft",
            ["submitted_data"] = request.SubmittedData,
            ["eligibility_result"] = request.EligibilityResult,
            ["workflow_version_id"] = workflowVersionId
        };

        var row = await _db.InsertAsync<JsonElement>("cases", caseData, ct);
        var caseId = row.GetProperty("id").GetString()!;

        // Create audit log entry
        await InsertAuditEntry(caseId, userId, "citizen", "case_created", null, "draft", "Case created", ct);

        // Get service name for response
        var serviceEl = await _db.GetByIdAsync<JsonElement>("services", request.ServiceId,
            select: "name,name_ar", cancellationToken: ct);
        JsonElement? service = serviceEl.ValueKind != JsonValueKind.Undefined ? serviceEl : null;

        return MapCaseResponse(row, service);
    }

    public async Task<List<PortalCaseResponse>> GetCasesAsync(string userId, string entityId, string? status = null, CancellationToken ct = default)
    {
        var filters = new List<string>
        {
            $"requester_user_id=eq.{userId}",
            $"entity_id=eq.{entityId}"
        };
        if (!string.IsNullOrEmpty(status))
            filters.Add($"status=eq.{status}");

        var rows = await _db.GetAsync<JsonElement>("cases",
            select: "id,reference_number,status,current_step,service_id,entity_id,workflow_version_id,submitted_data,eligibility_result,created_at,updated_at",
            filters: filters,
            order: "created_at.desc",
            cancellationToken: ct);

        // Batch lookup service names
        var serviceIds = rows.Select(r => r.GetProperty("service_id").GetString()!).Distinct().ToList();
        var serviceMap = new Dictionary<string, JsonElement>();
        foreach (var sid in serviceIds)
        {
            var svc = await _db.GetByIdAsync<JsonElement>("services", sid, select: "id,name,name_ar", cancellationToken: ct);
            if (svc.ValueKind != JsonValueKind.Undefined) serviceMap[sid] = svc;
        }

        return rows.Select(r =>
        {
            var sid = r.GetProperty("service_id").GetString()!;
            serviceMap.TryGetValue(sid, out var svc);
            JsonElement? svcEl = svc.ValueKind != JsonValueKind.Undefined ? svc : null;
            return MapCaseResponse(r, svcEl);
        }).ToList();
    }

    public async Task<PortalCaseDetailResponse?> GetCaseAsync(string caseId, string userId, CancellationToken ct = default)
    {
        var r = await _db.GetByIdAsync<JsonElement>("cases", caseId, cancellationToken: ct);
        if (r.ValueKind == JsonValueKind.Undefined) return null;

        // Verify ownership
        if (r.GetProperty("requester_user_id").GetString() != userId)
            return null;

        var serviceId = r.GetProperty("service_id").GetString()!;
        var serviceEl = await _db.GetByIdAsync<JsonElement>("services", serviceId,
            select: "name,name_ar,category", cancellationToken: ct);
        JsonElement? service = serviceEl.ValueKind != JsonValueKind.Undefined ? serviceEl : null;

        // Get workflow state if case has a workflow
        WorkflowStateResponse? workflowState = null;
        if (r.GetProp("workflow_version_id") != null)
        {
            workflowState = await _workflowService.GetWorkflowStateAsync(caseId, userId, ct);
        }

        return new PortalCaseDetailResponse(
            Id: r.GetProperty("id").GetString()!,
            ReferenceNumber: r.GetProperty("reference_number").GetString()!,
            Status: r.GetProperty("status").GetString()!,
            CurrentStep: r.GetProp("current_step"),
            ServiceId: serviceId,
            ServiceName: service?.GetProp("name") ?? "",
            ServiceNameAr: service?.GetProp("name_ar"),
            ServiceCategory: service?.GetProp("category"),
            EntityId: r.GetProperty("entity_id").GetString()!,
            WorkflowVersionId: r.GetProp("workflow_version_id"),
            SubmittedData: r.GetJsonProp("submitted_data"),
            EligibilityResult: r.GetJsonProp("eligibility_result"),
            WorkflowState: workflowState,
            CreatedAt: r.GetProperty("created_at").GetDateTimeOffset(),
            UpdatedAt: r.GetProperty("updated_at").GetDateTimeOffset()
        );
    }

    public async Task<PortalCaseResponse> UpdateCaseStatusAsync(string caseId, string userId, UpdateCaseStatusRequest request, CancellationToken ct = default)
    {
        // Get current case
        var existing = await _db.GetByIdAsync<JsonElement>("cases", caseId, cancellationToken: ct);
        if (existing.ValueKind == JsonValueKind.Undefined)
            throw new ATMET.AI.Core.Exceptions.NotFoundException($"Case {caseId} not found");

        var previousStatus = existing.GetProperty("status").GetString();

        var row = await _db.UpdateAsync<JsonElement>("cases", caseId,
            new { status = request.Status }, cancellationToken: ct);

        await InsertAuditEntry(caseId, userId, "citizen", "status_change",
            previousStatus, request.Status, request.Comment, ct);

        var serviceId = row.GetProperty("service_id").GetString()!;
        var serviceEl = await _db.GetByIdAsync<JsonElement>("services", serviceId,
            select: "name,name_ar", cancellationToken: ct);
        JsonElement? service = serviceEl.ValueKind != JsonValueKind.Undefined ? serviceEl : null;

        return MapCaseResponse(row, service);
    }

    private async Task InsertAuditEntry(string caseId, string userId, string role, string actionType,
        string? previousStatus, string? newStatus, string? comment, CancellationToken ct)
    {
        await _db.InsertAsync<JsonElement>("case_audit_log", new Dictionary<string, object?>
        {
            ["case_id"] = caseId,
            ["actor_user_id"] = userId,
            ["actor_role"] = role,
            ["action_type"] = actionType,
            ["previous_status"] = previousStatus,
            ["new_status"] = newStatus,
            ["comment"] = comment,
            ["action_payload"] = new { }
        }, ct);
    }

    private static PortalCaseResponse MapCaseResponse(JsonElement r, JsonElement? service)
    {
        return new PortalCaseResponse(
            Id: r.GetProperty("id").GetString()!,
            ReferenceNumber: r.GetProperty("reference_number").GetString()!,
            Status: r.GetProperty("status").GetString()!,
            CurrentStep: r.GetProp("current_step"),
            ServiceId: r.GetProperty("service_id").GetString()!,
            ServiceName: service?.GetProp("name") ?? "",
            ServiceNameAr: service?.GetProp("name_ar"),
            EntityId: r.GetProperty("entity_id").GetString()!,
            WorkflowVersionId: r.GetProp("workflow_version_id"),
            SubmittedData: r.GetJsonProp("submitted_data"),
            EligibilityResult: r.GetJsonProp("eligibility_result"),
            CreatedAt: r.GetProperty("created_at").GetDateTimeOffset(),
            UpdatedAt: r.GetProperty("updated_at").GetDateTimeOffset()
        );
    }
}
