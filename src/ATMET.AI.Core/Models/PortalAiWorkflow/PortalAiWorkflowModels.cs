using System.Text.Json;

namespace ATMET.AI.Core.Models.PortalAiWorkflow;

/// <summary>Normalized workflow turn status values for portal / SSE consumers.</summary>
public static class PortalAiWorkflowStatuses
{
    public const string Completed = "completed";
    public const string PausedForHitl = "paused_for_hitl";
    public const string Incomplete = "incomplete";
    public const string Failed = "failed";
}

/// <summary>
/// Thread state sent to Foundry (service_id, case_id, current_step, language).
/// When <paramref name="Language"/> is null or whitespace, the orchestrator merges from the conversation row.
/// </summary>
public record PortalAiThreadState(
    string? ServiceId,
    string? CaseId,
    string? CurrentStep,
    string? Language,
    string? LastAgent = null);

/// <summary>
/// First user turn for a portal Foundry workflow run.
/// </summary>
public record PortalAiWorkflowStartRequest(
    string UserMessage,
    PortalAiThreadState ThreadState,
    IReadOnlyList<string>? Attachments = null);

/// <summary>
/// Resume after a HITL pause (previous_response_id + resume payload).
/// </summary>
public record PortalAiWorkflowResumeRequest(
    string PreviousResponseId,
    JsonElement ResumePayload);

/// <summary>
/// Normalized outcome from Foundry after one run/resume call (to be mapped to SSE envelope).
/// <paramref name="AssistantOutput"/> is plain text from <c>ResponseResult.GetOutputText()</c> for portal persistence.
/// </summary>
public record PortalAiWorkflowTurnResult(
    string Status,
    string? AssistantOutput,
    string? RunId,
    string? ProjectConversationId,
    string? LastResponseId,
    string? UiAction,
    string? WaitingFor,
    JsonElement? RawPausePayload);
