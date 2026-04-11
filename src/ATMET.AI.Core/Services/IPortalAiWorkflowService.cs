using ATMET.AI.Core.Models.PortalAiWorkflow;

namespace ATMET.AI.Core.Services;

/// <summary>
/// Orchestrates Microsoft Foundry Project Responses workflow runs for the portal AI assistant (entity-agnostic; agent names from configuration).
/// Implementations call <c>AIProjectClient.ProjectOpenAIClient</c> and map pause/resume payloads to <see cref="PortalAiWorkflowTurnResult"/>.
/// </summary>
public interface IPortalAiWorkflowService
{
    /// <summary>
    /// Starts or continues a workflow turn from a user message (builds Foundry conversation state from DB).
    /// </summary>
    Task<PortalAiWorkflowTurnResult> StartOrContinueAsync(
        string conversationId,
        string userId,
        string entityId,
        PortalAiWorkflowStartRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes the workflow after a structured client action (form, documents, officer action, etc.).
    /// </summary>
    Task<PortalAiWorkflowTurnResult> ResumeAsync(
        string conversationId,
        string userId,
        string entityId,
        PortalAiWorkflowResumeRequest request,
        CancellationToken cancellationToken = default);
}
