
using ATMET.AI.Core.Exceptions;
using ATMET.AI.Core.Models.Portal;
using ATMET.AI.Infrastructure.Clients;
using ATMET.AI.Core.Models.PortalAiWorkflow;
using ATMET.AI.Core.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ATMET.AI.Infrastructure.Services.Portal;

/// <summary>
/// Portal AI chat: persists user messages, loads conversation context, and runs each turn through
/// <see cref="IPortalAiWorkflowService"/> (Foundry Project Responses + configured workflow agent).
/// </summary>
public class PortalAgentService : IPortalAgentService
{
    private readonly SupabaseRestClient _db;
    private readonly IPortalCatalogService _catalogService;
    private readonly IPortalCaseService _caseService;
    private readonly IPortalConversationService _conversationService;
    private readonly IPortalDocumentService _documentService;
    private readonly IPortalFormService _formService;
    private readonly IPortalWorkflowService _workflowService;
    private readonly IPortalAiWorkflowService _portalAiWorkflow;
    private readonly ILogger<PortalAgentService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public PortalAgentService(
        SupabaseRestClient db,
        IPortalCatalogService catalogService,
        IPortalCaseService caseService,
        IPortalConversationService conversationService,
        IPortalDocumentService documentService,
        IPortalFormService formService,
        IPortalWorkflowService workflowService,
        IPortalAiWorkflowService portalAiWorkflow,
        ILogger<PortalAgentService> logger)
    {
        _db = db;
        _catalogService = catalogService;
        _caseService = caseService;
        _conversationService = conversationService;
        _documentService = documentService;
        _formService = formService;
        _workflowService = workflowService;
        _portalAiWorkflow = portalAiWorkflow;
        _logger = logger;
    }

    public async IAsyncEnumerable<PortalChatEvent> ProcessMessageAsync(
        string conversationId,
        string userId,
        string entityId,
        PortalChatMessage userMessage,
        string language = "en",
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing portal chat message for conversation {ConversationId}, type={Type}",
            conversationId, userMessage.Type);

        await PersistMessageAsync(conversationId, userMessage, cancellationToken);

        var context = await BuildConversationContextAsync(conversationId, userId, entityId, cancellationToken);

        await foreach (var evt in ProcessPortalAiWorkflowAsync(
                           conversationId, userId, entityId, userMessage, language, context, cancellationToken))
            yield return evt;
    }

    /// <summary>
    /// Foundry workflow path: one Project Responses turn, rich reads via <see cref="IFoundryAgentReadService"/>,
    /// session persisted with <see cref="IPortalConversationService.UpdateFoundrySessionAsync"/> (no browser call to internal HTTP routes).
    /// </summary>
    private async IAsyncEnumerable<PortalChatEvent> ProcessPortalAiWorkflowAsync(
        string conversationId,
        string userId,
        string entityId,
        PortalChatMessage userMessage,
        string language,
        ConversationContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new PortalChatEvent(PortalChatEventTypes.Typing, null, null, null);

        var turnKind = string.Equals(userMessage.Type, PortalMessageTypes.WorkflowResume, StringComparison.OrdinalIgnoreCase)
            ? "resume"
            : "start_continue";
        using var workflowLogScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["AtmetPortalConversationId"] = conversationId,
            ["AtmetPortalEntityId"] = entityId,
            ["AtmetWorkflowTurnKind"] = turnKind,
            ["AtmetPortalMessageType"] = userMessage.Type
        });
        var sw = Stopwatch.StartNew();

        var agentInput = BuildAgentInput(userMessage, context);
        var threadState = new PortalAiThreadState(
            ServiceId: context.ServiceId,
            CaseId: context.CaseId,
            CurrentStep: context.CaseDetail?.CurrentStep ?? context.WorkflowState?.CurrentStepId,
            Language: language);

        PortalChatMessage? workflowError = null;
        PortalAiWorkflowTurnResult? result = null;
        try
        {
            if (string.Equals(userMessage.Type, PortalMessageTypes.WorkflowResume, StringComparison.OrdinalIgnoreCase))
            {
                var resumeRequest = BuildWorkflowResumeRequest(userMessage);
                result = await _portalAiWorkflow.ResumeAsync(
                    conversationId,
                    userId,
                    entityId,
                    resumeRequest,
                    cancellationToken);
            }
            else
            {
                result = await _portalAiWorkflow.StartOrContinueAsync(
                    conversationId,
                    userId,
                    entityId,
                    new PortalAiWorkflowStartRequest(agentInput, threadState, userMessage.Attachments),
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Portal AI workflow failed for conversation {AtmetPortalConversationId} after {AtmetWorkflowDurationMs}ms",
                conversationId, sw.ElapsedMilliseconds);
            workflowError = new PortalChatMessage(
                Id: Guid.NewGuid().ToString(),
                Role: "system",
                Type: PortalMessageTypes.Error,
                Content: null,
                Data: JsonSerializer.SerializeToElement(new ChatErrorData(
                    Code: ex is InvalidOperationException ? "workflow_resume_invalid" : "workflow_error",
                    Message: ex is NotFoundException ? ex.Message
                        : ex is InvalidOperationException ? ex.Message
                        : "Workflow processing failed.",
                    Retryable: ex is not NotFoundException && ex is not InvalidOperationException
                ), JsonOptions),
                Timestamp: DateTimeOffset.UtcNow);
        }

        if (workflowError != null)
        {
            yield return new PortalChatEvent(PortalChatEventTypes.Error, workflowError, null, null);
            yield return new PortalChatEvent(PortalChatEventTypes.Done, null, null, null);
            yield break;
        }

        sw.Stop();
        _logger.LogInformation(
            "Portal AI workflow SSE turn completed for conversation {AtmetPortalConversationId} in {AtmetWorkflowDurationMs}ms workflowStatus={AtmetWorkflowStatus}",
            conversationId, sw.ElapsedMilliseconds, result!.Status);

        var output = result!.AssistantOutput ?? string.Empty;
        var assistantMsg = ParseAssistantMessage(output);
        await PersistMessageAsync(conversationId, assistantMsg, cancellationToken);
        yield return new PortalChatEvent(PortalChatEventTypes.Message, assistantMsg, null, null);
        yield return new PortalChatEvent(PortalChatEventTypes.Done, null, null, null);
    }

    private record ConversationContext(
        string? CaseId,
        string? ServiceId,
        PortalCaseDetailResponse? CaseDetail,
        PortalServiceDetailResponse? ServiceDetail,
        WorkflowStateResponse? WorkflowState,
        List<DocumentChecklistItemResponse>? DocumentChecklist
    );

    private async Task<ConversationContext> BuildConversationContextAsync(
        string conversationId, string userId, string entityId, CancellationToken ct)
    {
        var conv = await _conversationService.GetConversationAsync(conversationId, userId, ct)
            ?? throw new NotFoundException($"Conversation {conversationId} not found");

        PortalCaseDetailResponse? caseDetail = null;
        PortalServiceDetailResponse? serviceDetail = null;
        WorkflowStateResponse? workflowState = null;
        List<DocumentChecklistItemResponse>? checklist = null;

        if (conv.CaseId != null)
        {
            caseDetail = await _caseService.GetCaseAsync(conv.CaseId, userId, ct);

            if (caseDetail != null)
            {
                workflowState = await _workflowService.GetWorkflowStateAsync(conv.CaseId, userId, ct);
                checklist = await _documentService.GetDocumentChecklistAsync(conv.CaseId, userId, ct);
            }
        }

        if (conv.ServiceId != null)
        {
            serviceDetail = await _catalogService.GetServiceAsync(conv.ServiceId, ct);
        }
        else if (caseDetail != null)
        {
            serviceDetail = await _catalogService.GetServiceAsync(caseDetail.ServiceId, ct);
        }

        return new ConversationContext(
            CaseId: conv.CaseId,
            ServiceId: conv.ServiceId ?? caseDetail?.ServiceId,
            CaseDetail: caseDetail,
            ServiceDetail: serviceDetail,
            WorkflowState: workflowState,
            DocumentChecklist: checklist
        );
    }

    private static string BuildAgentInput(PortalChatMessage userMessage, ConversationContext context)
    {
        if (userMessage.Type == PortalMessageTypes.Text)
            return userMessage.Content ?? "";

        return userMessage.Type switch
        {
            PortalMessageTypes.SelectService =>
                $"The user wants to start an application for service ID: {GetDataProperty(userMessage.Data, "serviceId")}. " +
                "Create a new case for this service and guide them through the process.",

            PortalMessageTypes.FormSubmit =>
                $"The user has submitted form data for case {GetDataProperty(userMessage.Data, "caseId")}. " +
                $"Form values: {userMessage.Data?.ToString() ?? "{}"}. Update the form data and validate it.",

            PortalMessageTypes.ConfirmAutofill =>
                GetDataProperty(userMessage.Data, "accepted") == "true"
                    ? $"The user has accepted the autofilled form values for case {GetDataProperty(userMessage.Data, "caseId")}."
                    : $"The user has rejected the autofilled form values for case {GetDataProperty(userMessage.Data, "caseId")}. " +
                      $"Adjusted fields: {GetDataProperty(userMessage.Data, "adjustedFields")}",

            PortalMessageTypes.DocumentAttached =>
                $"The user has uploaded a document '{GetDataProperty(userMessage.Data, "fileName")}' " +
                $"for case {GetDataProperty(userMessage.Data, "caseId")}. " +
                "Check the document checklist status and guide them on next steps.",

            PortalMessageTypes.WorkflowResume =>
                userMessage.Content ?? "",

            _ => userMessage.Content ?? JsonSerializer.Serialize(userMessage, JsonOptions)
        };
    }

    private static PortalAiWorkflowResumeRequest BuildWorkflowResumeRequest(PortalChatMessage msg)
    {
        if (msg.Data is not JsonElement data || data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            throw new InvalidOperationException("workflow_resume requires message data with previousResponseId.");

        var resumeData = JsonSerializer.Deserialize<PortalAiWorkflowResumeData>(
            data.GetRawText(),
            new JsonSerializerOptions(JsonOptions) { PropertyNameCaseInsensitive = true });

        if (resumeData is null || string.IsNullOrWhiteSpace(resumeData.PreviousResponseId))
            throw new InvalidOperationException("data.previousResponseId is required.");

        JsonElement payload;
        if (resumeData.ResumePayload is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } client)
        {
            var dict = new Dictionary<string, object> { ["client"] = client };
            if (!string.IsNullOrEmpty(msg.Content))
                dict["followUpText"] = msg.Content!;

            payload = JsonSerializer.SerializeToElement(dict, JsonOptions);
        }
        else if (!string.IsNullOrEmpty(msg.Content))
            payload = JsonSerializer.SerializeToElement(new { followUpText = msg.Content }, JsonOptions);
        else
            payload = JsonSerializer.SerializeToElement(new { }, JsonOptions);

        return new PortalAiWorkflowResumeRequest(resumeData.PreviousResponseId, payload);
    }

    private static string GetDataProperty(JsonElement? data, string propertyName)
    {
        if (data == null || data.Value.ValueKind == JsonValueKind.Undefined) return "";
        return data.Value.TryGetProperty(propertyName, out var prop) ? prop.ToString() : "";
    }

    private async Task PersistMessageAsync(string conversationId, PortalChatMessage message, CancellationToken ct)
    {
        var conv = await _db.GetByIdAsync<JsonElement>("conversations", conversationId, cancellationToken: ct);
        if (conv.ValueKind == JsonValueKind.Undefined) return;

        var messages = new List<JsonElement>();
        if (conv.TryGetProperty("messages", out var existing) && existing.ValueKind == JsonValueKind.Array)
        {
            messages = existing.EnumerateArray().ToList();
        }

        var msgDict = new Dictionary<string, object?>
        {
            ["id"] = message.Id,
            ["role"] = message.Role,
            ["type"] = message.Type,
            ["content"] = message.Content,
            ["timestamp"] = message.Timestamp.ToString("o")
        };

        if (message.Data.HasValue && message.Data.Value.ValueKind != JsonValueKind.Undefined)
            msgDict["data"] = JsonSerializer.Deserialize<object>(message.Data.Value.GetRawText());

        if (message.Attachments != null)
            msgDict["attachments"] = message.Attachments;

        messages.Add(JsonSerializer.SerializeToElement(msgDict));

        await _db.UpdateAsync<JsonElement>("conversations", conversationId,
            new { messages }, cancellationToken: ct);
    }

    private static PortalChatMessage ParseAssistantMessage(string content)
    {
        try
        {
            var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.TryGetProperty("type", out var typeProp) &&
                root.TryGetProperty("data", out var dataProp))
            {
                return new PortalChatMessage(
                    Id: Guid.NewGuid().ToString(),
                    Role: "assistant",
                    Type: typeProp.GetString() ?? PortalMessageTypes.Text,
                    Content: root.TryGetProperty("content", out var c) ? c.GetString() : null,
                    Data: dataProp,
                    Timestamp: DateTimeOffset.UtcNow);
            }
        }
        catch (JsonException)
        {
            // Not JSON — treat as plain text
        }

        return new PortalChatMessage(
            Id: Guid.NewGuid().ToString(),
            Role: "assistant",
            Type: PortalMessageTypes.Text,
            Content: content,
            Data: null,
            Timestamp: DateTimeOffset.UtcNow);
    }
}
