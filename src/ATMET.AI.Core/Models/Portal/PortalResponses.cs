using System.Text.Json;

namespace ATMET.AI.Core.Models.Portal;

// ==================================================================================
// Portal Response DTOs — returned by `/api/v1/portal/*` (camelCase on the wire).
// ==================================================================================

#region Services

/// <summary>
/// Lightweight row for **service catalog** lists (bilingual labels, SLA, category).
/// </summary>
public record PortalServiceResponse(
    string Id,
    string Name,
    string? NameAr,
    string? Description,
    string? DescriptionAr,
    string? Category,
    int SlaDays,
    bool IsActive
);

/// <summary>
/// Full service definition for **wizard setup**, including JSON blobs for dynamic UI and AI.
/// </summary>
public record PortalServiceDetailResponse(
    string Id,
    string Name,
    string? NameAr,
    string? Description,
    string? DescriptionAr,
    string? Category,
    int SlaDays,
    bool IsActive,
    JsonElement? FormSchema,
    JsonElement? RequiredDocuments,
    JsonElement? Intents
);

/// <summary>
/// Workflow metadata bound to a service (versioned definition + opaque config).
/// </summary>
public record PortalServiceWorkflowResponse(
    string? WorkflowId,
    string? WorkflowName,
    string? WorkflowNameAr,
    string? WorkflowVersionId,
    int? Version,
    JsonElement? Config
);

#endregion

#region Cases

/// <summary>
/// Case summary for **lists** (“My applications”).
/// </summary>
public record PortalCaseResponse(
    string Id,
    string ReferenceNumber,
    string Status,
    string? CurrentStep,
    string ServiceId,
    string ServiceName,
    string? ServiceNameAr,
    string EntityId,
    string? WorkflowVersionId,
    JsonElement? SubmittedData,
    JsonElement? EligibilityResult,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

/// <summary>
/// Case **detail** including embedded workflow snapshot for rich detail screens.
/// </summary>
public record PortalCaseDetailResponse(
    string Id,
    string ReferenceNumber,
    string Status,
    string? CurrentStep,
    string ServiceId,
    string ServiceName,
    string? ServiceNameAr,
    string? ServiceCategory,
    string EntityId,
    string? WorkflowVersionId,
    JsonElement? SubmittedData,
    JsonElement? EligibilityResult,
    WorkflowStateResponse? WorkflowState,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

#endregion

#region Conversations

/// <summary>
/// One row in the **conversation list** UI.
/// </summary>
public record PortalConversationSummaryResponse(
    string Id,
    string? Title,
    string Status,
    string? CaseId,
    string? ServiceId,
    string? LastMessage,
    DateTimeOffset? LastMessageAt,
    int MessageCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

/// <summary>
/// Full thread including **messages** and optional persisted **formData** snapshot.
/// </summary>
public record PortalConversationResponse(
    string Id,
    string? Title,
    string Status,
    string? CaseId,
    string? ServiceId,
    List<PortalMessageResponse> Messages,
    JsonElement? FormData,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

/// <summary>
/// A single persisted **chat message** (citizen, assistant, or system).
/// </summary>
public record PortalMessageResponse(
    string Id,
    string Role,
    string Content,
    string? Type,
    DateTimeOffset Timestamp,
    JsonElement? Attachments = null,
    JsonElement? DocumentScan = null,
    JsonElement? FormCard = null
);

#endregion

#region Documents

/// <summary>
/// Uploaded file metadata plus a **downloadable URL** (often signed / time-limited).
/// </summary>
public record PortalDocumentResponse(
    string Id,
    string CaseId,
    string? DocumentCatalogId,
    string FileName,
    string FileUrl,
    long? FileSize,
    string? MimeType,
    string ValidationStatus,
    string? Notes,
    DateTimeOffset CreatedAt
);

/// <summary>
/// One **required document** slot from the catalog with fulfillment status for UX checklists.
/// </summary>
public record DocumentChecklistItemResponse(
    string DocumentCatalogId,
    string NameEn,
    string NameAr,
    bool IsRequired,
    string UploadStatus,
    string? UploadedDocumentId,
    string? UploadedFileName
);

#endregion

#region Forms

/// <summary>
/// **Form schema** and current values for rendering and autosave.
/// </summary>
public record PortalFormResponse(
    JsonElement? FormSchema,
    JsonElement? CurrentData,
    string? ServiceId,
    string? CaseId
);

/// <summary>
/// Result of a **validate** call: validity, field errors, optional eligibility JSON.
/// </summary>
public record FormValidationResponse(
    bool IsValid,
    List<FormValidationError>? Errors,
    JsonElement? EligibilityResult = null
);

/// <summary>
/// Single validation error keyed by **field** id/path.
/// </summary>
public record FormValidationError(
    string Field,
    string Message
);

#endregion

#region Workflow

/// <summary>
/// Computed **workflow progress** for a case.
/// </summary>
public record WorkflowStateResponse(
    string? CurrentStepId,
    int ProgressPercent,
    int TotalSteps,
    int CompletedSteps,
    List<WorkflowStepStatusResponse> Steps
);

/// <summary>
/// Status of an individual **workflow step** (pending, active, completed, …).
/// </summary>
public record WorkflowStepStatusResponse(
    string Id,
    string Title,
    string? TitleAr,
    string? Description,
    string? DescriptionAr,
    string Status,
    DateTimeOffset? CompletedAt
);

#endregion

#region Activity

/// <summary>
/// One **audit / activity** entry for timelines.
/// </summary>
public record ActivityEntryResponse(
    string Id,
    string ActionType,
    string? ActorUserId,
    string? ActorRole,
    string? Comment,
    string? PreviousStatus,
    string? NewStatus,
    JsonElement? ActionPayload,
    DateTimeOffset CreatedAt
);

#endregion
