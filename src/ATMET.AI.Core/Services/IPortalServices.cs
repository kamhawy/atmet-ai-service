using ATMET.AI.Core.Models.Portal;

namespace ATMET.AI.Core.Services;

/// <summary>
/// Service catalog operations for the Client Portal.
/// </summary>
public interface IPortalCatalogService
{
    Task<List<PortalServiceResponse>> GetServicesAsync(string entityId, CancellationToken ct = default);
    Task<PortalServiceDetailResponse?> GetServiceAsync(string serviceId, CancellationToken ct = default);
    Task<PortalServiceWorkflowResponse?> GetServiceWorkflowAsync(string serviceId, CancellationToken ct = default);
}

/// <summary>
/// Case (application) management for the Client Portal.
/// </summary>
public interface IPortalCaseService
{
    Task<PortalCaseResponse> CreateCaseAsync(CreateCaseRequest request, string userId, CancellationToken ct = default);
    Task<List<PortalCaseResponse>> GetCasesAsync(string userId, string entityId, string? status = null, CancellationToken ct = default);
    Task<PortalCaseDetailResponse?> GetCaseAsync(string caseId, string userId, CancellationToken ct = default);
    Task<PortalCaseResponse> UpdateCaseStatusAsync(string caseId, string userId, UpdateCaseStatusRequest request, CancellationToken ct = default);
}

/// <summary>
/// Conversation and message management for the Client Portal.
/// </summary>
public interface IPortalConversationService
{
    Task<List<PortalConversationSummaryResponse>> GetConversationsAsync(string userId, string entityId, CancellationToken ct = default);
    Task<PortalConversationResponse> CreateConversationAsync(CreateConversationRequest request, string userId, CancellationToken ct = default);
    Task<PortalConversationResponse?> GetConversationAsync(string conversationId, string userId, CancellationToken ct = default);
    Task DeleteConversationAsync(string conversationId, string userId, CancellationToken ct = default);
    Task<PortalMessageResponse> SendMessageAsync(string conversationId, string userId, SendMessageRequest request, CancellationToken ct = default);
}

/// <summary>
/// Document upload and checklist operations for the Client Portal.
/// </summary>
public interface IPortalDocumentService
{
    Task<PortalDocumentResponse> UploadDocumentAsync(string caseId, string userId, Stream fileStream, string fileName, string contentType, long fileSize, string? documentCatalogId, CancellationToken ct = default);
    Task<List<PortalDocumentResponse>> GetDocumentsAsync(string caseId, string userId, CancellationToken ct = default);
    Task<PortalDocumentResponse?> GetDocumentAsync(string caseId, string docId, string userId, CancellationToken ct = default);
    Task<List<DocumentChecklistItemResponse>> GetDocumentChecklistAsync(string caseId, string userId, CancellationToken ct = default);
}

/// <summary>
/// Form schema and data operations for the Client Portal.
/// </summary>
public interface IPortalFormService
{
    Task<PortalFormResponse?> GetFormAsync(string caseId, string userId, CancellationToken ct = default);
    Task<PortalFormResponse> UpdateFormDataAsync(string caseId, string userId, UpdateFormDataRequest request, CancellationToken ct = default);
    Task<FormValidationResponse> ValidateFormAsync(string caseId, string userId, ValidateFormRequest request, CancellationToken ct = default);
    Task<PortalCaseResponse> SubmitFormAsync(string caseId, string userId, SubmitFormRequest request, CancellationToken ct = default);
}

/// <summary>
/// Workflow state and step progression for the Client Portal.
/// </summary>
public interface IPortalWorkflowService
{
    Task<WorkflowStateResponse?> GetWorkflowStateAsync(string caseId, string userId, CancellationToken ct = default);
    Task<WorkflowStateResponse> CompleteStepAsync(string caseId, string stepId, string userId, CompleteStepRequest? request = null, CancellationToken ct = default);
}

/// <summary>
/// Activity/audit log for the Client Portal.
/// </summary>
public interface IPortalActivityService
{
    Task<List<ActivityEntryResponse>> GetActivityAsync(string caseId, string userId, CancellationToken ct = default);
}
