using System.Text.Json;

namespace ATMET.AI.Core.Models.Portal;

// ==================================================================================
// Portal Response DTOs
// ==================================================================================

#region Services

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

public record PortalFormResponse(
    JsonElement? FormSchema,
    JsonElement? CurrentData,
    string? ServiceId,
    string? CaseId
);

public record FormValidationResponse(
    bool IsValid,
    List<FormValidationError>? Errors,
    JsonElement? EligibilityResult = null
);

public record FormValidationError(
    string Field,
    string Message
);

#endregion

#region Workflow

public record WorkflowStateResponse(
    string? CurrentStepId,
    int ProgressPercent,
    int TotalSteps,
    int CompletedSteps,
    List<WorkflowStepStatusResponse> Steps
);

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
