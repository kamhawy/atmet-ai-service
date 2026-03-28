using System.Text.Json;

namespace ATMET.AI.Core.Models.Portal;

// ==================================================================================
// Portal Request DTOs
// ==================================================================================

public record CreateCaseRequest(
    string ServiceId,
    string EntityId,
    JsonElement? SubmittedData = null,
    JsonElement? EligibilityResult = null
);

public record UpdateCaseStatusRequest(
    string Status,
    string? Comment = null
);

public record CreateConversationRequest(
    string EntityId,
    string? CaseId = null,
    string? ServiceId = null,
    string? Title = null
);

public record SendMessageRequest(
    string Content,
    string? Type = null
);

public record UpdateFormDataRequest(
    JsonElement FormData
);

public record ValidateFormRequest(
    JsonElement FormData
);

public record SubmitFormRequest(
    JsonElement? FormData = null,
    string? Comment = null
);

public record CompleteStepRequest(
    string? Comment = null
);
