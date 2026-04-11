using ATMET.AI.Core.Exceptions;
using ATMET.AI.Core.Models.Portal;
using ATMET.AI.Core.Models.PortalAiWorkflow;
using ATMET.AI.Core.Services;
using ATMET.AI.Infrastructure.Clients;
using ATMET.AI.Infrastructure.Configuration;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ATMET.AI.Infrastructure.Services.PortalAiWorkflow;

/// <summary>
/// Foundry Project Responses orchestration for the configured workflow agent. Sends PDF-aligned turn JSON
/// (<c>user_message</c>, <c>thread_state</c>, …) and persists session fields through
/// <see cref="IPortalConversationService.UpdateFoundrySessionAsync"/>.
/// </summary>
public class PortalAiWorkflowService : IPortalAiWorkflowService
{
    private static readonly JsonSerializerOptions WorkflowTurnJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNamingPolicy = null
    };

    private readonly AzureAIClientFactory _clientFactory;
    private readonly IPortalConversationService _conversationService;
    private readonly SupabaseRestClient _db;
    private readonly AzureAIOptions _options;
    private readonly ILogger<PortalAiWorkflowService> _logger;

    public PortalAiWorkflowService(
        AzureAIClientFactory clientFactory,
        IPortalConversationService conversationService,
        SupabaseRestClient db,
        IOptions<AzureAIOptions> options,
        ILogger<PortalAiWorkflowService> logger)
    {
        _clientFactory = clientFactory;
        _conversationService = conversationService;
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<PortalAiWorkflowTurnResult> StartOrContinueAsync(
        string conversationId,
        string userId,
        string entityId,
        PortalAiWorkflowStartRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteTurnAsync(
            conversationId,
            userId,
            entityId,
            request.ThreadState,
            request.UserMessage,
            explicitPreviousResponseId: null,
            resumePayload: null,
            request.Attachments,
            cancellationToken);

    /// <inheritdoc />
    public Task<PortalAiWorkflowTurnResult> ResumeAsync(
        string conversationId,
        string userId,
        string entityId,
        PortalAiWorkflowResumeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PreviousResponseId))
            throw new ArgumentException("PreviousResponseId is required.", nameof(request));

        return ExecuteTurnAsync(
            conversationId,
            userId,
            entityId,
            new PortalAiThreadState(null, null, null, null),
            userNaturalLanguageInput: string.Empty,
            explicitPreviousResponseId: request.PreviousResponseId,
            resumePayload: request.ResumePayload,
            attachmentIds: null,
            cancellationToken);
    }

    private async Task<PortalAiWorkflowTurnResult> ExecuteTurnAsync(
        string conversationId,
        string userId,
        string entityId,
        PortalAiThreadState threadState,
        string userNaturalLanguageInput,
        string? explicitPreviousResponseId,
        JsonElement? resumePayload,
        IReadOnlyList<string>? attachmentIds,
        CancellationToken cancellationToken)
    {
        var turnKind = explicitPreviousResponseId != null ? "resume" : "start_continue";
        using var logScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["AtmetConversationId"] = conversationId,
            ["AtmetEntityId"] = entityId,
            ["AtmetWorkflowTurnKind"] = turnKind
        });
        var sw = Stopwatch.StartNew();

        using var activity = PortalAiWorkflowTelemetry.Source.StartActivity("PortalWorkflow.ExecuteTurn", ActivityKind.Internal);
        activity?.SetTag("atmet.conversation_id", conversationId);
        activity?.SetTag("atmet.entity_id", entityId);
        activity?.SetTag("atmet.turn_kind", turnKind);

        await EnsureConversationTenantAsync(conversationId, userId, entityId, cancellationToken);

        var conv = await _conversationService.GetConversationAsync(conversationId, userId, cancellationToken)
                   ?? throw new NotFoundException($"Conversation {conversationId} not found");

        var merged = new PortalAiThreadState(
            ServiceId: threadState.ServiceId ?? conv.ServiceId,
            CaseId: threadState.CaseId ?? conv.CaseId,
            CurrentStep: threadState.CurrentStep ?? conv.FoundryCurrentStep,
            Language: string.IsNullOrWhiteSpace(threadState.Language)
                ? (conv.ConversationLanguage ?? "en")
                : threadState.Language,
            LastAgent: threadState.LastAgent);

        // Only chain previous_response_id when reusing an existing Foundry project conversation.
        var hadProjectConversation = !string.IsNullOrEmpty(conv.FoundryProjectConversationId);
        var chainPrevious = explicitPreviousResponseId
                            ?? (hadProjectConversation ? conv.LastResponseId : null);

        var augmentedMessage = BuildWorkflowTurnInputJson(
            merged,
            userNaturalLanguageInput,
            resumePayload,
            attachmentIds);

        var projectClient = _clientFactory.GetProjectClient();
        var openAi = projectClient.ProjectOpenAIClient;
        var agentReference = new AgentReference(_options.WorkflowAgentName, _options.WorkflowAgentVersion);

        var projectConversationId = conv.FoundryProjectConversationId ?? string.Empty;
        var createdNewProjectConversation = false;
        if (string.IsNullOrEmpty(projectConversationId))
        {
            var created = (await openAi.GetProjectConversationsClient()
                .CreateProjectConversationAsync(cancellationToken: cancellationToken)).Value;
            projectConversationId = created.Id;
            createdNewProjectConversation = true;
            _logger.LogInformation(
                "Created Foundry project conversation {FoundryConversationId} for portal conversation {ConversationId}",
                projectConversationId, conversationId);
        }

        var responseClient = openAi.GetProjectResponsesClientForAgent(agentReference, projectConversationId);

        OpenAI.Responses.ResponseResult response;
        if (!string.IsNullOrEmpty(chainPrevious))
        {
            response = (await responseClient.CreateResponseAsync(
                    augmentedMessage,
                    previousResponseId: chainPrevious,
                    cancellationToken: cancellationToken))
                .Value;
        }
        else
        {
            response = (await responseClient.CreateResponseAsync(augmentedMessage, cancellationToken: cancellationToken))
                .Value;
        }

        var outputText = response.GetOutputText();
        var (status, uiAction, waitingFor, runId, rawPause) =
            PortalAiWorkflowResponseMapper.Map(response, outputText, resumePayload);

        var clearPause = status != PortalAiWorkflowStatuses.PausedForHitl;

        var patch = new FoundryConversationSessionPatch(
            FoundryProjectConversationId: projectConversationId,
            FoundryRunId: runId,
            LastResponseId: response.Id,
            PauseUiAction: uiAction,
            PauseWaitingFor: waitingFor,
            PauseEnvelope: clearPause ? null : rawPause,
            FoundryCurrentStep: merged.CurrentStep,
            ConversationLanguage: merged.Language,
            ClearPauseFields: clearPause);

        var updated = await _conversationService.UpdateFoundrySessionAsync(
            conversationId,
            userId,
            patch,
            cancellationToken);

        if (updated == null)
            _logger.LogWarning("UpdateFoundrySessionAsync returned null for conversation {ConversationId}", conversationId);

        sw.Stop();
        var chainedPrevious = !string.IsNullOrEmpty(chainPrevious);
        activity?.SetTag("atmet.foundry_project_conversation_id", projectConversationId);
        activity?.SetTag("atmet.foundry_response_id", response.Id);
        activity?.SetTag("atmet.workflow_status", status);
        activity?.SetTag("atmet.workflow_duration_ms", sw.ElapsedMilliseconds.ToString());

        _logger.LogInformation(
            "Portal AI workflow turn finished: conversation={AtmetConversationId} entity={AtmetEntityId} foundryConversation={AtmetFoundryProjectConversationId} response={AtmetFoundryResponseId} status={AtmetWorkflowStatus} durationMs={AtmetWorkflowDurationMs} chainedPrevious={AtmetChainedPreviousResponse} newFoundryConversation={AtmetNewFoundryConversation}",
            conversationId, entityId, projectConversationId, response.Id, status, sw.ElapsedMilliseconds, chainedPrevious, createdNewProjectConversation);

        return new PortalAiWorkflowTurnResult(
            Status: status,
            AssistantOutput: string.IsNullOrEmpty(outputText) ? null : outputText,
            RunId: runId,
            ProjectConversationId: projectConversationId,
            LastResponseId: response.Id,
            UiAction: uiAction,
            WaitingFor: waitingFor,
            RawPausePayload: rawPause);
    }

    private async Task EnsureConversationTenantAsync(
        string conversationId,
        string userId,
        string entityId,
        CancellationToken cancellationToken)
    {
        var rows = await _db.GetAsync<JsonElement>(
            "conversations",
            select: "id",
            filters:
            [
                $"id=eq.{conversationId}",
                $"user_id=eq.{userId}",
                $"entity_id=eq.{entityId}"
            ],
            limit: 1,
            cancellationToken: cancellationToken);

        if (rows.Count == 0)
            throw new NotFoundException($"Conversation {conversationId} not found");
    }

    /// <summary>
    /// Tax Assistant PDF §2 entry payload: <c>user_message</c>, <c>language</c>, <c>thread_state</c>, <c>attachments</c>,
    /// optional <c>resume_payload</c> on HITL resume (§6–§7).
    /// </summary>
    /// <remarks>
    /// <paramref name="attachmentIds"/> — ATMET convention: each entry is a <strong>portal document id</strong> (UUID string)
    /// returned from <c>POST /api/v1/portal/cases/{{caseId}}/documents</c> after the SPA uploads binary to storage.
    /// Matches the PDF intent that <c>attachments</c> are <em>references</em> to uploaded artifacts, not embedded file bytes.
    /// </remarks>
    private static string BuildWorkflowTurnInputJson(
        PortalAiThreadState merged,
        string userNaturalLanguageInput,
        JsonElement? resumePayload,
        IReadOnlyList<string>? attachmentIds)
    {
        var userMessageText = userNaturalLanguageInput ?? string.Empty;
        if (string.IsNullOrEmpty(userMessageText) &&
            resumePayload is { ValueKind: JsonValueKind.Object } rp &&
            rp.TryGetProperty("followUpText", out var ft) &&
            ft.ValueKind == JsonValueKind.String)
        {
            userMessageText = ft.GetString() ?? string.Empty;
        }

        var threadState = new Dictionary<string, object?>
        {
            ["service_id"] = merged.ServiceId,
            ["case_id"] = merged.CaseId,
            ["current_step"] = merged.CurrentStep,
            ["last_agent"] = merged.LastAgent
        };

        var root = new Dictionary<string, object?>
        {
            ["user_message"] = userMessageText,
            ["language"] = string.IsNullOrWhiteSpace(merged.Language) ? "en" : merged.Language,
            ["thread_state"] = threadState,
            ["attachments"] = attachmentIds?.Where(static s => !string.IsNullOrWhiteSpace(s)).ToArray() ?? []
        };

        if (resumePayload is { ValueKind: JsonValueKind.Object } rpv && rpv.EnumerateObject().Any())
            root["resume_payload"] = JsonSerializer.Deserialize<object>(rpv.GetRawText());

        return JsonSerializer.Serialize(root, WorkflowTurnJsonOptions);
    }
}
