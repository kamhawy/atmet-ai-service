using System.Text.Json;

namespace ATMET.AI.Core.Models.Portal;

// ==================================================================================
// Portal Request DTOs — JSON bodies for `/api/v1/portal/*` (camelCase in wire format).
// ==================================================================================

/// <summary>
/// Creates a new citizen **case** (application) for a service within an entity/tenant.
/// </summary>
/// <param name="ServiceId">UUID of the target service row (`services.id`).</param>
/// <param name="EntityId">UUID of the government entity for multi-tenant isolation (`entities.id`).</param>
/// <param name="SubmittedData">Optional JSON snapshot of wizard fields captured before persistence.</param>
/// <param name="EligibilityResult">Optional JSON from a pre-screen (rules engine / AI) to store on the case.</param>
public record CreateCaseRequest(
    string ServiceId,
    string EntityId,
    JsonElement? SubmittedData = null,
    JsonElement? EligibilityResult = null
);

/// <summary>
/// Administrative or automated **status transition** on a case.
/// </summary>
/// <param name="Status">Target status text (aligns with platform case status vocabulary, e.g. submitted, under_review).</param>
/// <param name="Comment">Optional human-readable note stored on the audit trail.</param>
public record UpdateCaseStatusRequest(
    string Status,
    string? Comment = null
);

/// <summary>
/// Opens a **portal conversation** thread, optionally bound to a case or service.
/// </summary>
/// <param name="EntityId">Tenant UUID; must match portal routing context.</param>
/// <param name="CaseId">Optional case UUID to associate chat with an in-flight application.</param>
/// <param name="ServiceId">Optional service UUID for intent routing before a case exists.</param>
/// <param name="Title">Optional display title for the conversation list UI.</param>
public record CreateConversationRequest(
    string EntityId,
    string? CaseId = null,
    string? ServiceId = null,
    string? Title = null
);

/// <summary>
/// Appends a **simple chat message** to a conversation (non-SSE API).
/// </summary>
/// <param name="Content">Plain text or serialized payload depending on client convention.</param>
/// <param name="Type">Optional message discriminator for structured UIs.</param>
public record SendMessageRequest(
    string Content,
    string? Type = null
);

/// <summary>
/// Partial **form autosave** — merges into the case’s stored JSON field bag.
/// </summary>
/// <param name="FormData">Object-shaped JSON whose keys are field ids / JSON paths.</param>
public record UpdateFormDataRequest(
    JsonElement FormData
);

/// <summary>
/// **Validate-only** run against service rules without committing a final submission.
/// </summary>
/// <param name="FormData">Candidate field values to validate.</param>
public record ValidateFormRequest(
    JsonElement FormData
);

/// <summary>
/// **Final submission** of the application form for a case.
/// </summary>
/// <param name="FormData">Optional override payload; if null, server uses last saved data.</param>
/// <param name="Comment">Optional citizen comment accompanying submission.</param>
public record SubmitFormRequest(
    JsonElement? FormData = null,
    string? Comment = null
);

/// <summary>
/// Completes a **workflow step** in a guided process.
/// </summary>
/// <param name="Comment">Optional note recorded on the activity log.</param>
public record CompleteStepRequest(
    string? Comment = null
);
