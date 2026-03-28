using ATMET.AI.Core.Models.Portal;
using ATMET.AI.Core.Services;
using ATMET.AI.Infrastructure.Clients;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ATMET.AI.Infrastructure.Services.Portal;

public class PortalWorkflowService : IPortalWorkflowService
{
    private readonly SupabaseRestClient _db;
    private readonly ILogger<PortalWorkflowService> _logger;

    public PortalWorkflowService(SupabaseRestClient db, ILogger<PortalWorkflowService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<WorkflowStateResponse?> GetWorkflowStateAsync(string caseId, string userId, CancellationToken ct = default)
    {
        var caseRow = await _db.GetByIdAsync<JsonElement>("cases", caseId,
            select: "id,workflow_version_id,current_step,requester_user_id",
            cancellationToken: ct);
        if (caseRow.ValueKind == JsonValueKind.Undefined) return null;

        if (caseRow.GetProperty("requester_user_id").GetString() != userId)
            return null;

        var workflowVersionId = caseRow.GetProp("workflow_version_id");
        if (workflowVersionId == null) return null;

        // Get workflow config
        var version = await _db.GetByIdAsync<JsonElement>("workflow_versions", workflowVersionId,
            select: "config", cancellationToken: ct);
        if (version.ValueKind == JsonValueKind.Undefined) return null;

        var config = version.GetJsonProp("config");
        if (config == null) return null;

        // Extract steps from workflow config
        var steps = ExtractSteps(config.Value);
        if (steps.Count == 0) return null;

        var currentStepId = caseRow.GetProp("current_step");

        // Get execution log to determine completed steps
        var execLogs = await _db.GetAsync<JsonElement>("workflow_execution_log",
            select: "step_id,status,completed_at",
            filters: [$"case_id=eq.{caseId}"],
            order: "started_at.asc",
            cancellationToken: ct);

        var completedSteps = execLogs
            .Where(e => e.GetProp("status") == "completed")
            .Select(e => e.GetProperty("step_id").GetString()!)
            .ToHashSet();

        var completedAtMap = execLogs
            .Where(e => e.GetProp("status") == "completed")
            .ToDictionary(
                e => e.GetProperty("step_id").GetString()!,
                e => e.GetDateProp("completed_at"));

        // Build step status list
        bool foundActive = false;
        var stepStatuses = steps.Select(s =>
        {
            string status;
            DateTimeOffset? completedAt = null;

            if (completedSteps.Contains(s.Id))
            {
                status = "completed";
                completedAtMap.TryGetValue(s.Id, out completedAt);
            }
            else if (!foundActive && (currentStepId == null || s.Id == currentStepId))
            {
                status = "active";
                foundActive = true;
            }
            else
            {
                status = foundActive ? "pending" : "completed"; // Steps before active are completed
            }

            return new WorkflowStepStatusResponse(
                Id: s.Id,
                Title: s.Title,
                TitleAr: s.TitleAr,
                Description: s.Description,
                DescriptionAr: s.DescriptionAr,
                Status: status,
                CompletedAt: completedAt
            );
        }).ToList();

        var completedCount = stepStatuses.Count(s => s.Status == "completed");
        var progressPercent = steps.Count > 0 ? (int)(completedCount * 100.0 / steps.Count) : 0;

        return new WorkflowStateResponse(
            CurrentStepId: currentStepId ?? steps.FirstOrDefault(s => !completedSteps.Contains(s.Id))?.Id,
            ProgressPercent: progressPercent,
            TotalSteps: steps.Count,
            CompletedSteps: completedCount,
            Steps: stepStatuses
        );
    }

    public async Task<WorkflowStateResponse> CompleteStepAsync(string caseId, string stepId, string userId, CompleteStepRequest? request = null, CancellationToken ct = default)
    {
        var caseRow = await _db.GetByIdAsync<JsonElement>("cases", caseId,
            select: "id,workflow_version_id,current_step,requester_user_id,status",
            cancellationToken: ct);
        if (caseRow.ValueKind == JsonValueKind.Undefined)
            throw new ATMET.AI.Core.Exceptions.NotFoundException($"Case {caseId} not found");

        if (caseRow.GetProp("requester_user_id") != userId)
            throw new ATMET.AI.Core.Exceptions.ForbiddenException("You do not have access to this case");

        // Insert execution log entry
        await _db.InsertAsync<JsonElement>("workflow_execution_log", new Dictionary<string, object?>
        {
            ["case_id"] = caseId,
            ["workflow_version_id"] = caseRow.GetProp("workflow_version_id"),
            ["step_id"] = stepId,
            ["step_name"] = stepId,
            ["executor_type"] = "citizen",
            ["executor_id"] = userId,
            ["action"] = "complete",
            ["status"] = "completed",
            ["started_at"] = DateTimeOffset.UtcNow,
            ["completed_at"] = DateTimeOffset.UtcNow
        }, ct);

        // Determine next step
        var workflowVersionId = caseRow.GetProp("workflow_version_id");
        string? nextStepId = null;
        if (workflowVersionId != null)
        {
            var ver = await _db.GetByIdAsync<JsonElement>("workflow_versions", workflowVersionId,
                select: "config", cancellationToken: ct);
            if (ver.ValueKind != JsonValueKind.Undefined)
            {
                var steps = ExtractSteps(ver.GetJsonProp("config")!.Value);
                var currentIndex = steps.FindIndex(s => s.Id == stepId);
                if (currentIndex >= 0 && currentIndex < steps.Count - 1)
                    nextStepId = steps[currentIndex + 1].Id;
            }
        }

        // Update case current_step
        await _db.UpdateAsync<JsonElement>("cases", caseId,
            new { current_step = nextStepId }, cancellationToken: ct);

        // Audit log
        await _db.InsertAsync<JsonElement>("case_audit_log", new Dictionary<string, object?>
        {
            ["case_id"] = caseId,
            ["actor_user_id"] = userId,
            ["actor_role"] = "citizen",
            ["action_type"] = "step_completed",
            ["comment"] = request?.Comment ?? $"Step {stepId} completed",
            ["action_payload"] = new { step_id = stepId, next_step_id = nextStepId }
        }, ct);

        _logger.LogInformation("Step {StepId} completed for case {CaseId}", stepId, caseId);

        return (await GetWorkflowStateAsync(caseId, userId, ct))!;
    }

    private record StepInfo(string Id, string Title, string? TitleAr, string? Description, string? DescriptionAr);

    private static List<StepInfo> ExtractSteps(JsonElement config)
    {
        var steps = new List<StepInfo>();

        // Try config.stages[] pattern (WorkflowConfig structure)
        if (config.TryGetProperty("stages", out var stages) && stages.ValueKind == JsonValueKind.Array)
        {
            foreach (var stage in stages.EnumerateArray())
            {
                var id = stage.GetProp("id") ?? stage.GetProp("stageId") ?? Guid.NewGuid().ToString();
                var title = stage.GetProp("name") ?? stage.GetProp("title") ?? id;
                var titleAr = stage.GetProp("name_ar") ?? stage.GetProp("nameAr");
                var desc = stage.GetProp("description");
                var descAr = stage.GetProp("description_ar") ?? stage.GetProp("descriptionAr");
                steps.Add(new StepInfo(id, title, titleAr, desc, descAr));
            }
        }
        // Try config.steps[] pattern
        else if (config.TryGetProperty("steps", out var stepsEl) && stepsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var step in stepsEl.EnumerateArray())
            {
                var id = step.GetProp("id") ?? step.GetProp("stepId") ?? Guid.NewGuid().ToString();
                var title = step.GetProp("name") ?? step.GetProp("title") ?? id;
                var titleAr = step.GetProp("name_ar") ?? step.GetProp("nameAr");
                var desc = step.GetProp("description");
                var descAr = step.GetProp("description_ar") ?? step.GetProp("descriptionAr");
                steps.Add(new StepInfo(id, title, titleAr, desc, descAr));
            }
        }

        return steps;
    }
}
