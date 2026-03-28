using ATMET.AI.Core.Models.Portal;
using ATMET.AI.Core.Services;
using ATMET.AI.Infrastructure.Clients;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ATMET.AI.Infrastructure.Services.Portal;

public class PortalFormService : IPortalFormService
{
    private readonly SupabaseRestClient _db;
    private readonly ILogger<PortalFormService> _logger;

    public PortalFormService(SupabaseRestClient db, ILogger<PortalFormService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<PortalFormResponse?> GetFormAsync(string caseId, string userId, CancellationToken ct = default)
    {
        var caseRow = await _db.GetByIdAsync<JsonElement>("cases", caseId,
            select: "id,service_id,submitted_data,requester_user_id", cancellationToken: ct);
        if (caseRow.ValueKind == JsonValueKind.Undefined) return null;

        if (caseRow.GetProperty("requester_user_id").GetString() != userId)
            return null;

        var serviceId = caseRow.GetProperty("service_id").GetString()!;

        // Get form schema from the service
        var service = await _db.GetByIdAsync<JsonElement>("services", serviceId,
            select: "id,form_schema", cancellationToken: ct);

        return new PortalFormResponse(
            FormSchema: service.ValueKind != JsonValueKind.Undefined ? service.GetJsonProp("form_schema") : null,
            CurrentData: caseRow.GetJsonProp("submitted_data"),
            ServiceId: serviceId,
            CaseId: caseId
        );
    }

    public async Task<PortalFormResponse> UpdateFormDataAsync(string caseId, string userId, UpdateFormDataRequest request, CancellationToken ct = default)
    {
        // Get current case to verify ownership and merge data
        var caseRow = await _db.GetByIdAsync<JsonElement>("cases", caseId,
            select: "id,service_id,submitted_data,requester_user_id", cancellationToken: ct);
        if (caseRow.ValueKind == JsonValueKind.Undefined)
            throw new ATMET.AI.Core.Exceptions.NotFoundException($"Case {caseId} not found");
        if (caseRow.GetProperty("requester_user_id").GetString() != userId)
            throw new ATMET.AI.Core.Exceptions.NotFoundException($"Case {caseId} not found");

        // Merge new form data with existing submitted_data
        var existing = caseRow.GetJsonProp("submitted_data");
        var merged = MergeJsonData(existing, request.FormData);

        await _db.UpdateAsync<JsonElement>("cases", caseId,
            new { submitted_data = merged }, cancellationToken: ct);

        var serviceId = caseRow.GetProperty("service_id").GetString()!;
        var service = await _db.GetByIdAsync<JsonElement>("services", serviceId,
            select: "id,form_schema", cancellationToken: ct);

        return new PortalFormResponse(
            FormSchema: service.ValueKind != JsonValueKind.Undefined ? service.GetJsonProp("form_schema") : null,
            CurrentData: merged,
            ServiceId: serviceId,
            CaseId: caseId
        );
    }

    public async Task<FormValidationResponse> ValidateFormAsync(string caseId, string userId, ValidateFormRequest request, CancellationToken ct = default)
    {
        // Get case and service form schema
        var caseRow = await _db.GetByIdAsync<JsonElement>("cases", caseId,
            select: "id,service_id,requester_user_id", cancellationToken: ct);
        if (caseRow.ValueKind == JsonValueKind.Undefined)
            throw new ATMET.AI.Core.Exceptions.NotFoundException($"Case {caseId} not found");
        if (caseRow.GetProperty("requester_user_id").GetString() != userId)
            throw new ATMET.AI.Core.Exceptions.NotFoundException($"Case {caseId} not found");

        var serviceId = caseRow.GetProperty("service_id").GetString()!;

        // Get form schema to validate required fields
        var service = await _db.GetByIdAsync<JsonElement>("services", serviceId,
            select: "form_schema", cancellationToken: ct);

        var errors = new List<FormValidationError>();

        if (service.ValueKind != JsonValueKind.Undefined)
        {
            var schema = service.GetJsonProp("form_schema");
            if (schema != null && schema.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var field in schema.Value.EnumerateArray())
                {
                    var fieldName = field.GetProp("name") ?? field.GetProp("id") ?? "";
                    var isRequired = field.TryGetProperty("required", out var req) && req.GetBoolean();

                    if (isRequired && !string.IsNullOrEmpty(fieldName))
                    {
                        if (!request.FormData.TryGetProperty(fieldName, out var val) ||
                            val.ValueKind == JsonValueKind.Null ||
                            (val.ValueKind == JsonValueKind.String && string.IsNullOrEmpty(val.GetString())))
                        {
                            var label = field.GetProp("label") ?? fieldName;
                            errors.Add(new FormValidationError(fieldName, $"{label} is required"));
                        }
                    }
                }
            }
        }

        return new FormValidationResponse(
            IsValid: errors.Count == 0,
            Errors: errors.Count > 0 ? errors : null
        );
    }

    public async Task<PortalCaseResponse> SubmitFormAsync(string caseId, string userId, SubmitFormRequest request, CancellationToken ct = default)
    {
        // Get current case
        var caseRow = await _db.GetByIdAsync<JsonElement>("cases", caseId,
            select: "id,service_id,submitted_data,status,requester_user_id,entity_id,reference_number,current_step,workflow_version_id,eligibility_result,created_at,updated_at",
            cancellationToken: ct);
        if (caseRow.ValueKind == JsonValueKind.Undefined)
            throw new ATMET.AI.Core.Exceptions.NotFoundException($"Case {caseId} not found");
        if (caseRow.GetProperty("requester_user_id").GetString() != userId)
            throw new ATMET.AI.Core.Exceptions.NotFoundException($"Case {caseId} not found");

        var previousStatus = caseRow.GetProperty("status").GetString();

        // Merge any final form data
        var updateData = new Dictionary<string, object?> { ["status"] = "submitted" };

        if (request.FormData != null)
        {
            var existing = caseRow.GetJsonProp("submitted_data");
            updateData["submitted_data"] = MergeJsonData(existing, request.FormData.Value);
        }

        var row = await _db.UpdateAsync<JsonElement>("cases", caseId, updateData, cancellationToken: ct);

        // Audit log
        await _db.InsertAsync<JsonElement>("case_audit_log", new Dictionary<string, object?>
        {
            ["case_id"] = caseId,
            ["actor_user_id"] = userId,
            ["actor_role"] = "citizen",
            ["action_type"] = "submit",
            ["previous_status"] = previousStatus,
            ["new_status"] = "submitted",
            ["comment"] = request.Comment ?? "Application submitted",
            ["action_payload"] = new { }
        }, ct);

        var serviceId = row.GetProperty("service_id").GetString()!;
        var serviceEl = await _db.GetByIdAsync<JsonElement>("services", serviceId,
            select: "name,name_ar", cancellationToken: ct);
        JsonElement? service = serviceEl.ValueKind != JsonValueKind.Undefined ? serviceEl : null;

        return new PortalCaseResponse(
            Id: row.GetProperty("id").GetString()!,
            ReferenceNumber: row.GetProperty("reference_number").GetString()!,
            Status: row.GetProperty("status").GetString()!,
            CurrentStep: row.GetProp("current_step"),
            ServiceId: serviceId,
            ServiceName: service?.GetProp("name") ?? "",
            ServiceNameAr: service?.GetProp("name_ar"),
            EntityId: row.GetProperty("entity_id").GetString()!,
            WorkflowVersionId: row.GetProp("workflow_version_id"),
            SubmittedData: row.GetJsonProp("submitted_data"),
            EligibilityResult: row.GetJsonProp("eligibility_result"),
            CreatedAt: row.GetProperty("created_at").GetDateTimeOffset(),
            UpdatedAt: row.GetProperty("updated_at").GetDateTimeOffset()
        );
    }

    private static JsonElement MergeJsonData(JsonElement? existing, JsonElement newData)
    {
        if (existing == null || existing.Value.ValueKind != JsonValueKind.Object)
            return newData;

        var merged = new Dictionary<string, JsonElement>();

        foreach (var prop in existing.Value.EnumerateObject())
            merged[prop.Name] = prop.Value.Clone();

        if (newData.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in newData.EnumerateObject())
                merged[prop.Name] = prop.Value.Clone();
        }

        return JsonSerializer.SerializeToElement(merged);
    }
}
