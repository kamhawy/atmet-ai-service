using System.Text.Json;

namespace ATMET.AI.Core.Models.Portal;

// ==================================================================================
// Portal Chat Message & Event DTOs
// ==================================================================================

/// <summary>
/// A single message in a portal conversation. Uses a discriminated union pattern:
/// the <see cref="Type"/> field determines the shape of <see cref="Data"/>.
/// </summary>
public record PortalChatMessage(
    string Id,
    string Role,        // "user" | "assistant" | "system"
    string Type,        // discriminated union key — see PortalMessageTypes
    string? Content,    // text content (primarily for type=text)
    JsonElement? Data,  // typed payload — shape determined by Type
    DateTimeOffset Timestamp,
    string[]? Attachments = null
);

/// <summary>
/// A single SSE event streamed from the portal chat endpoint.
/// </summary>
public record PortalChatEvent(
    string EventType,               // "typing" | "message" | "tool_call" | "done" | "error"
    PortalChatMessage? Message,     // present when EventType is "message"
    string? ToolName,               // present when EventType is "tool_call"
    string? ToolStatus              // "calling" | "completed" — present when EventType is "tool_call"
);

/// <summary>
/// Constants for portal chat message types (discriminated union keys).
/// </summary>
public static class PortalMessageTypes
{
    // ── Agent → User ────────────────────────────────────────────────────────
    public const string Text = "text";
    public const string ServiceCatalog = "service_catalog";
    public const string CaseCreated = "case_created";
    public const string FormRequest = "form_request";
    public const string FormConfirmed = "form_confirmed";
    public const string DocumentRequest = "document_request";
    public const string DocumentScanned = "document_scanned";
    public const string FormAutofilled = "form_autofilled";
    public const string WorkflowUpdate = "workflow_update";
    public const string StatusUpdate = "status_update";
    public const string EligibilityResult = "eligibility_result";
    public const string Error = "error";

    // ── User → Agent ────────────────────────────────────────────────────────
    public const string SelectService = "select_service";
    public const string FormSubmit = "form_submit";
    public const string ConfirmAutofill = "confirm_autofill";
    public const string DocumentAttached = "document_attached";

    /// <summary>
    /// HITL resume after a Foundry pause: requires <see cref="PortalAiWorkflowResumeData"/> in <see cref="PortalChatMessage.Data"/>.
    /// </summary>
    public const string WorkflowResume = "workflow_resume";
}

/// <summary>
/// Constants for SSE event types streamed from the chat endpoint.
/// </summary>
public static class PortalChatEventTypes
{
    public const string Typing = "typing";
    public const string Message = "message";
    public const string ToolCall = "tool_call";
    public const string Done = "done";
    public const string Error = "error";
}

// ==================================================================================
// Typed Data Payloads — Agent → User
// ==================================================================================

public record ServiceCatalogData(
    List<PortalServiceResponse> Services
);

public record CaseCreatedData(
    string CaseId,
    string ReferenceNumber,
    string ServiceNameEn,
    string? ServiceNameAr,
    string Status
);

public record FormRequestData(
    string CaseId,
    string? StepId,
    JsonElement Fields,                         // array of form field definitions
    JsonElement? CurrentValues                  // { fieldName: value } object
);

public record FormConfirmedData(
    string CaseId,
    List<FormFieldSummary> SubmittedFields
);

public record FormFieldSummary(
    string Key,
    string Label,
    string Value
);

public record DocumentRequestData(
    string CaseId,
    List<DocumentChecklistItemResponse> Checklist
);

public record DocumentScannedData(
    string DocumentId,
    string FileName,
    List<ExtractedField> ExtractedFields
);

public record ExtractedField(
    string Key,
    string Label,
    string Value,
    double Confidence
);

public record FormAutofilledData(
    string CaseId,
    List<AutofilledField> AutofilledFields
);

public record AutofilledField(
    string FieldId,
    string Label,
    string? OldValue,
    string NewValue,
    string Source       // e.g. "passport_scan", "trade_license"
);

public record WorkflowUpdateData(
    string CaseId,
    WorkflowStepStatusResponse CurrentStep,
    List<WorkflowStepStatusResponse> AllSteps,
    int ProgressPercent
);

public record StatusUpdateData(
    string CaseId,
    string? PreviousStatus,
    string NewStatus,
    string? Comment
);

public record EligibilityResultData(
    bool Eligible,
    List<string> Reasons,
    List<string>? MissingRequirements = null
);

public record ChatErrorData(
    string Code,
    string Message,
    bool Retryable = false
);

// ==================================================================================
// Typed Data Payloads — User → Agent
// ==================================================================================

public record SelectServiceData(
    string ServiceId
);

public record FormSubmitData(
    string CaseId,
    JsonElement Values      // { fieldName: value } object
);

public record ConfirmAutofillData(
    string CaseId,
    bool Accepted,
    JsonElement? AdjustedFields = null      // optional overrides
);

public record DocumentAttachedData(
    string CaseId,
    string DocumentId,
    string? DocumentCatalogId,
    string FileName
);

/// <summary>
/// User → agent: resume a paused Foundry workflow turn (<c>previous_response_id</c> + optional structured payload).
/// </summary>
public record PortalAiWorkflowResumeData(
    string PreviousResponseId,
    JsonElement? ResumePayload = null);
