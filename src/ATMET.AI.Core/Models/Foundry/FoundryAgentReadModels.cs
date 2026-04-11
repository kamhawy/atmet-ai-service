using System.Text.Json;
using ATMET.AI.Core.Models.Portal;

namespace ATMET.AI.Core.Models.Foundry;

/// <summary>
/// Rich case payload for Foundry **tool HTTP** callers (API key + entity header). Not citizen-scoped by user id.
/// </summary>
public record CaseDetailForAgent(
    string Id,
    string ReferenceNumber,
    string Status,
    string? CurrentStep,
    string ServiceId,
    string ServiceName,
    string? ServiceNameAr,
    string? ServiceCategory,
    string EntityId,
    string RequesterUserId,
    string? WorkflowVersionId,
    JsonElement? SubmittedData,
    JsonElement? EligibilityResult,
    WorkflowStateResponse? WorkflowState,
    IReadOnlyList<ConversationSummaryForAgent> Conversations,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Conversation row subset for agent tools (Foundry session fields + linkage to case).
/// </summary>
public record ConversationSummaryForAgent(
    string Id,
    string? Title,
    string Status,
    string? FoundryProjectConversationId,
    string? FoundryRunId,
    string? LastResponseId,
    string? PauseUiAction,
    string? PauseWaitingFor,
    string? FoundryCurrentStep,
    string? ConversationLanguage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Service definition + workflow binding for Foundry tool HTTP callers.
/// </summary>
public record ServiceDetailForAgent(
    string Id,
    string Name,
    string? NameAr,
    string? Description,
    string? DescriptionAr,
    string? Category,
    int SlaDays,
    bool IsActive,
    string EntityId,
    JsonElement? FormSchema,
    JsonElement? RequiredDocuments,
    JsonElement? Intents,
    PortalServiceWorkflowResponse? Workflow);
