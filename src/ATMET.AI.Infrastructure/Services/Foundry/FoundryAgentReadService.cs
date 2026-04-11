using ATMET.AI.Core.Models.Foundry;
using ATMET.AI.Core.Models.Portal;
using ATMET.AI.Core.Services;
using ATMET.AI.Infrastructure.Clients;
using ATMET.AI.Infrastructure.Services.Portal;
using System.Text.Json;

namespace ATMET.AI.Infrastructure.Services.Foundry;

public class FoundryAgentReadService : IFoundryAgentReadService
{
    private const string ConversationSelectForAgent =
        "id,title,status,created_at,updated_at,foundry_project_conversation_id,foundry_run_id,last_response_id," +
        "pause_ui_action,pause_waiting_for,foundry_current_step,conversation_language";

    private readonly SupabaseRestClient _db;
    private readonly IPortalWorkflowService _workflowService;
    private readonly IPortalCatalogService _catalogService;

    public FoundryAgentReadService(
        SupabaseRestClient db,
        IPortalWorkflowService workflowService,
        IPortalCatalogService catalogService)
    {
        _db = db;
        _workflowService = workflowService;
        _catalogService = catalogService;
    }

    public async Task<CaseDetailForAgent?> GetCaseForAgentAsync(string caseId, string entityId,
        CancellationToken ct = default)
    {
        var rows = await _db.GetAsync<JsonElement>("cases",
            select:
            "id,reference_number,status,current_step,service_id,entity_id,requester_user_id,workflow_version_id,submitted_data,eligibility_result,created_at,updated_at",
            filters: [$"id=eq.{caseId}", $"entity_id=eq.{entityId}"],
            limit: 1,
            cancellationToken: ct);

        if (rows.Count == 0) return null;
        return await MapCaseDetailForAgentAsync(rows[0], entityId, ct);
    }

    public async Task<CaseDetailForAgent?> GetCaseByReferenceForAgentAsync(string referenceNumber, string entityId,
        CancellationToken ct = default)
    {
        var rows = await _db.GetAsync<JsonElement>("cases",
            select:
            "id,reference_number,status,current_step,service_id,entity_id,requester_user_id,workflow_version_id,submitted_data,eligibility_result,created_at,updated_at",
            filters: [$"reference_number=eq.{referenceNumber}", $"entity_id=eq.{entityId}"],
            limit: 1,
            cancellationToken: ct);

        if (rows.Count == 0) return null;
        return await MapCaseDetailForAgentAsync(rows[0], entityId, ct);
    }

    public async Task<ServiceDetailForAgent?> GetServiceForAgentAsync(string serviceId, string entityId,
        CancellationToken ct = default)
    {
        var row = await _db.GetByIdAsync<JsonElement>("services", serviceId,
            select:
            "id,name,name_ar,description,description_ar,category,sla_days,is_active,entity_id,form_schema,required_documents,intents",
            cancellationToken: ct);
        if (row.ValueKind == JsonValueKind.Undefined) return null;

        if (row.GetProperty("entity_id").GetString() != entityId)
            return null;

        var workflow = await _catalogService.GetServiceWorkflowAsync(serviceId, ct);

        return new ServiceDetailForAgent(
            Id: row.GetProperty("id").GetString()!,
            Name: row.GetProperty("name").GetString()!,
            NameAr: row.GetProp("name_ar"),
            Description: row.GetProp("description"),
            DescriptionAr: row.GetProp("description_ar"),
            Category: row.GetProp("category"),
            SlaDays: row.GetProperty("sla_days").GetInt32(),
            IsActive: row.GetProperty("is_active").GetBoolean(),
            EntityId: row.GetProperty("entity_id").GetString()!,
            FormSchema: row.GetJsonProp("form_schema"),
            RequiredDocuments: row.GetJsonProp("required_documents"),
            Intents: row.GetJsonProp("intents"),
            Workflow: workflow);
    }

    private async Task<CaseDetailForAgent?> MapCaseDetailForAgentAsync(JsonElement r, string entityId,
        CancellationToken ct)
    {
        // Callers filter by entity_id; re-check in case this helper is reused later.
        if (r.GetProperty("entity_id").GetString() != entityId)
            return null;

        var caseId = r.GetProperty("id").GetString()!;
        var serviceId = r.GetProperty("service_id").GetString()!;
        var serviceEl = await _db.GetByIdAsync<JsonElement>("services", serviceId,
            select: "name,name_ar,category", cancellationToken: ct);
        JsonElement? service = serviceEl.ValueKind != JsonValueKind.Undefined ? serviceEl : null;

        WorkflowStateResponse? workflowState = null;
        if (r.GetProp("workflow_version_id") != null)
            workflowState = await _workflowService.GetWorkflowStateForEntityAsync(caseId, entityId, ct);

        var convRows = await _db.GetAsync<JsonElement>("conversations",
            select: ConversationSelectForAgent,
            filters: [$"case_id=eq.{caseId}", $"entity_id=eq.{entityId}"],
            order: "updated_at.desc",
            cancellationToken: ct);

        var conversations = convRows.Select(MapConversationSummary).ToList();

        return new CaseDetailForAgent(
            Id: caseId,
            ReferenceNumber: r.GetProperty("reference_number").GetString()!,
            Status: r.GetProperty("status").GetString()!,
            CurrentStep: r.GetProp("current_step"),
            ServiceId: serviceId,
            ServiceName: service?.GetProp("name") ?? "",
            ServiceNameAr: service?.GetProp("name_ar"),
            ServiceCategory: service?.GetProp("category"),
            EntityId: r.GetProperty("entity_id").GetString()!,
            RequesterUserId: r.GetProperty("requester_user_id").GetString()!,
            WorkflowVersionId: r.GetProp("workflow_version_id"),
            SubmittedData: r.GetJsonProp("submitted_data"),
            EligibilityResult: r.GetJsonProp("eligibility_result"),
            WorkflowState: workflowState,
            Conversations: conversations,
            CreatedAt: r.GetProperty("created_at").GetDateTimeOffset(),
            UpdatedAt: r.GetProperty("updated_at").GetDateTimeOffset());
    }

    private static ConversationSummaryForAgent MapConversationSummary(JsonElement r) =>
        new(
            Id: r.GetProperty("id").GetString()!,
            Title: r.GetProp("title"),
            Status: r.GetProperty("status").GetString()!,
            FoundryProjectConversationId: r.GetProp("foundry_project_conversation_id"),
            FoundryRunId: r.GetProp("foundry_run_id"),
            LastResponseId: r.GetProp("last_response_id"),
            PauseUiAction: r.GetProp("pause_ui_action"),
            PauseWaitingFor: r.GetProp("pause_waiting_for"),
            FoundryCurrentStep: r.GetProp("foundry_current_step"),
            ConversationLanguage: r.GetProp("conversation_language"),
            CreatedAt: r.GetProperty("created_at").GetDateTimeOffset(),
            UpdatedAt: r.GetProperty("updated_at").GetDateTimeOffset());
}
