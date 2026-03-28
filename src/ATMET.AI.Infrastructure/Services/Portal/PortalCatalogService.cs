using ATMET.AI.Core.Models.Portal;
using ATMET.AI.Core.Services;
using ATMET.AI.Infrastructure.Clients;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ATMET.AI.Infrastructure.Services.Portal;

public class PortalCatalogService : IPortalCatalogService
{
    private readonly SupabaseRestClient _db;
    private readonly ILogger<PortalCatalogService> _logger;

    public PortalCatalogService(SupabaseRestClient db, ILogger<PortalCatalogService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<PortalServiceResponse>> GetServicesAsync(string entityId, CancellationToken ct = default)
    {
        var rows = await _db.GetAsync<JsonElement>("services",
            select: "id,name,name_ar,description,description_ar,category,sla_days,is_active",
            filters: [$"entity_id=eq.{entityId}", "is_active=eq.true"],
            order: "name.asc",
            cancellationToken: ct);

        return rows.Select(r => new PortalServiceResponse(
            Id: r.GetProperty("id").GetString()!,
            Name: r.GetProperty("name").GetString()!,
            NameAr: r.GetProp("name_ar"),
            Description: r.GetProp("description"),
            DescriptionAr: r.GetProp("description_ar"),
            Category: r.GetProp("category"),
            SlaDays: r.GetProperty("sla_days").GetInt32(),
            IsActive: r.GetProperty("is_active").GetBoolean()
        )).ToList();
    }

    public async Task<PortalServiceDetailResponse?> GetServiceAsync(string serviceId, CancellationToken ct = default)
    {
        var row = await _db.GetByIdAsync<JsonElement>("services", serviceId, cancellationToken: ct);
        if (row.ValueKind == JsonValueKind.Undefined) return null;

        return new PortalServiceDetailResponse(
            Id: row.GetProperty("id").GetString()!,
            Name: row.GetProperty("name").GetString()!,
            NameAr: row.GetProp("name_ar"),
            Description: row.GetProp("description"),
            DescriptionAr: row.GetProp("description_ar"),
            Category: row.GetProp("category"),
            SlaDays: row.GetProperty("sla_days").GetInt32(),
            IsActive: row.GetProperty("is_active").GetBoolean(),
            FormSchema: row.GetJsonProp("form_schema"),
            RequiredDocuments: row.GetJsonProp("required_documents"),
            Intents: row.GetJsonProp("intents")
        );
    }

    public async Task<PortalServiceWorkflowResponse?> GetServiceWorkflowAsync(string serviceId, CancellationToken ct = default)
    {
        // Get the service_workflow_binding for this service
        var bindings = await _db.GetAsync<JsonElement>("service_workflow_binding",
            select: "id,service_id,workflow_id,workflow_version_id",
            filters: [$"service_id=eq.{serviceId}"],
            limit: 1,
            cancellationToken: ct);

        if (bindings.Count == 0) return null;

        var binding = bindings[0];
        var workflowId = binding.GetProp("workflow_id");
        var workflowVersionId = binding.GetProp("workflow_version_id");

        if (workflowId == null) return null;

        // Get workflow name
        var workflow = await _db.GetByIdAsync<JsonElement>("workflows", workflowId, cancellationToken: ct);
        string? workflowName = null, workflowNameAr = null;
        if (workflow.ValueKind != JsonValueKind.Undefined)
        {
            workflowName = workflow.GetProp("name_en");
            workflowNameAr = workflow.GetProp("name_ar");
        }

        // Get the active workflow version config
        JsonElement? config = null;
        int? version = null;
        string? versionId = workflowVersionId;

        if (workflowVersionId != null)
        {
            var ver = await _db.GetByIdAsync<JsonElement>("workflow_versions", workflowVersionId, cancellationToken: ct);
            if (ver.ValueKind != JsonValueKind.Undefined)
            {
                config = ver.GetJsonProp("config");
                version = ver.TryGetProperty("version", out var v) ? v.GetInt32() : null;
            }
        }
        else
        {
            // Fallback: get the active version for this workflow
            var versions = await _db.GetAsync<JsonElement>("workflow_versions",
                select: "id,version,config,is_active",
                filters: [$"workflow_id=eq.{workflowId}", "is_active=eq.true"],
                limit: 1,
                cancellationToken: ct);

            if (versions.Count > 0)
            {
                versionId = versions[0].GetProperty("id").GetString();
                config = versions[0].GetJsonProp("config");
                version = versions[0].TryGetProperty("version", out var v) ? v.GetInt32() : null;
            }
        }

        return new PortalServiceWorkflowResponse(
            WorkflowId: workflowId,
            WorkflowName: workflowName,
            WorkflowNameAr: workflowNameAr,
            WorkflowVersionId: versionId,
            Version: version,
            Config: config
        );
    }
}
