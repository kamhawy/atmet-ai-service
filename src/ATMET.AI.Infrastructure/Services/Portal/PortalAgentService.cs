
using ATMET.AI.Core.Exceptions;
using ATMET.AI.Core.Models.Portal;
using ATMET.AI.Core.Services;
using ATMET.AI.Infrastructure.Clients;
using ATMET.AI.Infrastructure.Configuration;
using Azure.AI.Agents.Persistent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ATMET.AI.Infrastructure.Services.Portal;

/// <summary>
/// Portal AI agent service that processes user messages, calls tools via Azure AI Foundry,
/// and streams structured responses. All portal state mutations flow through this service —
/// the agent decides what tools to call (create_case, update_form, etc.) based on user input.
/// </summary>
public class PortalAgentService : IPortalAgentService
{
    private readonly AzureAIClientFactory _clientFactory;
    private readonly SupabaseRestClient _db;
    private readonly IPortalCatalogService _catalogService;
    private readonly IPortalCaseService _caseService;
    private readonly IPortalConversationService _conversationService;
    private readonly IPortalDocumentService _documentService;
    private readonly IPortalFormService _formService;
    private readonly IPortalWorkflowService _workflowService;
    private readonly AzureAIOptions _aiOptions;
    private readonly ILogger<PortalAgentService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public PortalAgentService(
        AzureAIClientFactory clientFactory,
        SupabaseRestClient db,
        IPortalCatalogService catalogService,
        IPortalCaseService caseService,
        IPortalConversationService conversationService,
        IPortalDocumentService documentService,
        IPortalFormService formService,
        IPortalWorkflowService workflowService,
        IOptions<AzureAIOptions> aiOptions,
        ILogger<PortalAgentService> logger)
    {
        _clientFactory = clientFactory;
        _db = db;
        _catalogService = catalogService;
        _caseService = caseService;
        _conversationService = conversationService;
        _documentService = documentService;
        _formService = formService;
        _workflowService = workflowService;
        _aiOptions = aiOptions.Value;
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

        // 1. Persist the user message to the conversation
        await PersistMessageAsync(conversationId, userMessage, cancellationToken);

        // 2. Emit typing event
        yield return new PortalChatEvent(PortalChatEventTypes.Typing, null, null, null);

        // 3. Load conversation context (case, service, entity info)
        var context = await BuildConversationContextAsync(conversationId, userId, entityId, cancellationToken);

        // 4. Build the message content for the AI agent
        var agentInput = BuildAgentInput(userMessage, context);

        // 5. Get or create the AI agent thread for this conversation
        var agentsClient = _clientFactory.GetAgentsClient();
        var threadId = await GetOrCreateThreadAsync(agentsClient, conversationId, userId, cancellationToken);

        // 6. Add the user message to the agent thread
        await agentsClient.Messages.CreateMessageAsync(
            threadId: threadId,
            role: MessageRole.User,
            content: agentInput,
            cancellationToken: cancellationToken);

        // 7. Create a run with tool definitions
        var tools = BuildToolDefinitions();
        var systemPrompt = BuildSystemPrompt(context, entityId, language);

        var run = await agentsClient.Runs.CreateRunAsync(
            threadId: threadId,
            assistantId: await GetOrCreatePortalAgentAsync(agentsClient, cancellationToken),
            additionalInstructions: systemPrompt,
            overrideTools: tools,
            cancellationToken: cancellationToken);

        var runValue = run.Value;

        // 8. Poll run status, handle tool calls, and yield events
        while (!cancellationToken.IsCancellationRequested)
        {
            if (runValue.Status == RunStatus.Completed)
            {
                // Get the assistant's response messages
                var assistantMessages = await GetNewAssistantMessagesAsync(agentsClient, threadId, cancellationToken);

                foreach (var msg in assistantMessages)
                {
                    var chatMessage = ParseAssistantMessage(msg);
                    await PersistMessageAsync(conversationId, chatMessage, cancellationToken);

                    yield return new PortalChatEvent(PortalChatEventTypes.Message, chatMessage, null, null);
                }
                break;
            }
            else if (runValue.Status == RunStatus.RequiresAction)
            {
                // Handle tool calls
                var toolOutputs = new List<ToolOutput>();

                foreach (var toolCall in runValue.RequiredActions)
                {
                    if (toolCall is RequiredFunctionToolCall functionCall)
                    {
                        yield return new PortalChatEvent(
                            PortalChatEventTypes.ToolCall, null, functionCall.Name, "calling");

                        _logger.LogInformation("Agent calling tool: {ToolName} with args: {Args}",
                            functionCall.Name, functionCall.Arguments);

                        var result = await ExecuteToolAsync(
                            functionCall.Name, functionCall.Arguments, userId, entityId, cancellationToken);

                        toolOutputs.Add(new ToolOutput(functionCall.Id, result));

                        yield return new PortalChatEvent(
                            PortalChatEventTypes.ToolCall, null, functionCall.Name, "completed");
                    }
                }

                // Submit tool outputs and continue the run
                run = await agentsClient.Runs.SubmitToolOutputsToRunAsync(
                    runValue,
                    toolOutputs,
                    cancellationToken);
                runValue = run.Value;
            }
            else if (runValue.Status == RunStatus.Failed ||
                     runValue.Status == RunStatus.Cancelled ||
                     runValue.Status == RunStatus.Expired)
            {
                _logger.LogError("Agent run failed with status {Status}: {Error}",
                    runValue.Status, runValue.LastError?.Message);

                var errorMessage = new PortalChatMessage(
                    Id: Guid.NewGuid().ToString(),
                    Role: "system",
                    Type: PortalMessageTypes.Error,
                    Content: null,
                    Data: JsonSerializer.SerializeToElement(new ChatErrorData(
                        Code: runValue.Status.ToString(),
                        Message: runValue.LastError?.Message ?? "An unexpected error occurred.",
                        Retryable: runValue.Status != RunStatus.Cancelled
                    ), JsonOptions),
                    Timestamp: DateTimeOffset.UtcNow);

                yield return new PortalChatEvent(PortalChatEventTypes.Error, errorMessage, null, null);
                break;
            }
            else
            {
                // Still in progress — poll
                await Task.Delay(500, cancellationToken);
                run = await agentsClient.Runs.GetRunAsync(
                    threadId: threadId, runId: runValue.Id, cancellationToken: cancellationToken);
                runValue = run.Value;
            }
        }

        // 9. Emit done event
        yield return new PortalChatEvent(PortalChatEventTypes.Done, null, null, null);
    }

    // ========================================================================
    // Context Building
    // ========================================================================

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
        // For text messages, pass content directly
        if (userMessage.Type == PortalMessageTypes.Text)
            return userMessage.Content ?? "";

        // For structured messages, build a descriptive prompt
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

            _ => userMessage.Content ?? JsonSerializer.Serialize(userMessage, JsonOptions)
        };
    }

    private static string GetDataProperty(JsonElement? data, string propertyName)
    {
        if (data == null || data.Value.ValueKind == JsonValueKind.Undefined) return "";
        return data.Value.TryGetProperty(propertyName, out var prop) ? prop.ToString() : "";
    }

    private static string BuildSystemPrompt(ConversationContext context, string entityId, string language = "en")
    {
        var isArabic = language.Equals("ar", StringComparison.OrdinalIgnoreCase);
        var parts = new List<string>
        {
            $"You are a helpful government services assistant for entity '{entityId}'.",
            "You help citizens navigate government services, submit applications, upload documents, and track their progress.",
            "Always respond in a helpful, clear, and professional manner.",
            "When you perform actions (create cases, update forms, etc.), use the provided tools.",
            "After using a tool, summarize what you did in a clear way for the citizen.",
            "",
            $"LANGUAGE: The user's preferred language is {(isArabic ? "Arabic (العربية)" : "English")}. " +
            $"You MUST respond in {(isArabic ? "Arabic" : "English")}. " +
            (isArabic ? "Use formal Arabic (فصحى) suitable for government communication." : ""),
            "",
            "IMPORTANT: Your responses must be structured JSON messages when performing actions.",
            "For regular text responses, respond naturally.",
            "When you create a case, update a form, or perform any mutation, describe what happened clearly."
        };

        if (context.CaseDetail != null)
        {
            parts.Add($"\nCurrent case context:");
            parts.Add($"- Case ID: {context.CaseDetail.Id}");
            parts.Add($"- Reference: {context.CaseDetail.ReferenceNumber}");
            parts.Add($"- Status: {context.CaseDetail.Status}");
            parts.Add($"- Service: {context.CaseDetail.ServiceName}");
            if (context.CaseDetail.CurrentStep != null)
                parts.Add($"- Current step: {context.CaseDetail.CurrentStep}");
        }

        if (context.ServiceDetail != null)
        {
            parts.Add($"\nService context:");
            parts.Add($"- Service: {context.ServiceDetail.Name} ({context.ServiceDetail.NameAr})");
            parts.Add($"- Category: {context.ServiceDetail.Category}");
            parts.Add($"- SLA: {context.ServiceDetail.SlaDays} days");
            if (context.ServiceDetail.FormSchema.HasValue)
                parts.Add($"- Form schema available: yes");
        }

        if (context.WorkflowState != null)
        {
            parts.Add($"\nWorkflow state:");
            parts.Add($"- Progress: {context.WorkflowState.ProgressPercent}%");
            parts.Add($"- Current step: {context.WorkflowState.CurrentStepId}");
            parts.Add($"- Steps: {string.Join(", ", context.WorkflowState.Steps.Select(s => $"{s.Title} ({s.Status})"))}");
        }

        if (context.DocumentChecklist != null && context.DocumentChecklist.Count > 0)
        {
            parts.Add($"\nDocument checklist:");
            foreach (var doc in context.DocumentChecklist)
                parts.Add($"- {doc.NameEn}: {doc.UploadStatus} (required: {doc.IsRequired})");
        }

        return string.Join("\n", parts);
    }

    // ========================================================================
    // Tool Definitions
    // ========================================================================

    private static IEnumerable<ToolDefinition> BuildToolDefinitions()
    {
        yield return new FunctionToolDefinition(
            name: "list_services",
            description: "List available government services for the current entity. Call this when the user asks what services are available.",
            parameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "entity_id": { "type": "string", "description": "The entity ID to list services for" }
                },
                "required": ["entity_id"]
            }
            """));

        yield return new FunctionToolDefinition(
            name: "create_case",
            description: "Create a new case/application for a government service. Call this when the user wants to apply for a service.",
            parameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "service_id": { "type": "string", "description": "The service ID to create a case for" },
                    "entity_id": { "type": "string", "description": "The entity ID" }
                },
                "required": ["service_id", "entity_id"]
            }
            """));

        yield return new FunctionToolDefinition(
            name: "get_case",
            description: "Get details of a specific case/application including its current status and workflow state.",
            parameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "case_id": { "type": "string", "description": "The case ID to retrieve" }
                },
                "required": ["case_id"]
            }
            """));

        yield return new FunctionToolDefinition(
            name: "get_user_cases",
            description: "List all cases/applications for the current user. Call this when the user asks about their applications.",
            parameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "entity_id": { "type": "string", "description": "The entity ID" },
                    "status": { "type": "string", "description": "Optional status filter" }
                },
                "required": ["entity_id"]
            }
            """));

        yield return new FunctionToolDefinition(
            name: "update_form_data",
            description: "Update form field values for a case. Call this when the user provides information for their application form.",
            parameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "case_id": { "type": "string", "description": "The case ID" },
                    "form_data": { "type": "object", "description": "Key-value pairs of form field values" }
                },
                "required": ["case_id", "form_data"]
            }
            """));

        yield return new FunctionToolDefinition(
            name: "validate_form",
            description: "Validate form data against business rules for a case. Call this before submitting to check for errors.",
            parameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "case_id": { "type": "string", "description": "The case ID" },
                    "form_data": { "type": "object", "description": "Form data to validate" }
                },
                "required": ["case_id", "form_data"]
            }
            """));

        yield return new FunctionToolDefinition(
            name: "submit_form",
            description: "Submit the completed form for a case. This finalizes the form data and advances the workflow. Only call after validation passes.",
            parameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "case_id": { "type": "string", "description": "The case ID" },
                    "comment": { "type": "string", "description": "Optional submission comment" }
                },
                "required": ["case_id"]
            }
            """));

        yield return new FunctionToolDefinition(
            name: "get_document_checklist",
            description: "Get the list of required and optional documents for a case, including upload status.",
            parameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "case_id": { "type": "string", "description": "The case ID" }
                },
                "required": ["case_id"]
            }
            """));

        yield return new FunctionToolDefinition(
            name: "get_workflow_state",
            description: "Get the current workflow progress for a case, including all steps and their completion status.",
            parameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "case_id": { "type": "string", "description": "The case ID" }
                },
                "required": ["case_id"]
            }
            """));

        yield return new FunctionToolDefinition(
            name: "advance_workflow_step",
            description: "Complete the current workflow step and advance to the next one. Only call when all requirements for the step are met.",
            parameters: BinaryData.FromString("""
            {
                "type": "object",
                "properties": {
                    "case_id": { "type": "string", "description": "The case ID" },
                    "step_id": { "type": "string", "description": "The step ID to complete" },
                    "comment": { "type": "string", "description": "Optional completion comment" }
                },
                "required": ["case_id", "step_id"]
            }
            """));
    }

    // ========================================================================
    // Tool Execution
    // ========================================================================

    private async Task<string> ExecuteToolAsync(
        string toolName, string arguments, string userId, string entityId, CancellationToken ct)
    {
        try
        {
            var args = JsonDocument.Parse(arguments).RootElement;

            return toolName switch
            {
                "list_services" => await ExecuteListServicesAsync(args, entityId, ct),
                "create_case" => await ExecuteCreateCaseAsync(args, userId, entityId, ct),
                "get_case" => await ExecuteGetCaseAsync(args, userId, ct),
                "get_user_cases" => await ExecuteGetUserCasesAsync(args, userId, entityId, ct),
                "update_form_data" => await ExecuteUpdateFormDataAsync(args, userId, ct),
                "validate_form" => await ExecuteValidateFormAsync(args, userId, ct),
                "submit_form" => await ExecuteSubmitFormAsync(args, userId, ct),
                "get_document_checklist" => await ExecuteGetDocumentChecklistAsync(args, userId, ct),
                "get_workflow_state" => await ExecuteGetWorkflowStateAsync(args, userId, ct),
                "advance_workflow_step" => await ExecuteAdvanceWorkflowStepAsync(args, userId, ct),
                _ => JsonSerializer.Serialize(new { error = $"Unknown tool: {toolName}" })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool execution failed: {ToolName}", toolName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private async Task<string> ExecuteListServicesAsync(JsonElement args, string entityId, CancellationToken ct)
    {
        var eid = args.TryGetProperty("entity_id", out var e) ? e.GetString() ?? entityId : entityId;
        var services = await _catalogService.GetServicesAsync(eid, ct);
        return JsonSerializer.Serialize(services, JsonOptions);
    }

    private async Task<string> ExecuteCreateCaseAsync(JsonElement args, string userId, string entityId, CancellationToken ct)
    {
        var serviceId = args.GetProperty("service_id").GetString()!;
        var eid = args.TryGetProperty("entity_id", out var e) ? e.GetString() ?? entityId : entityId;

        var request = new CreateCaseRequest(ServiceId: serviceId, EntityId: eid);
        var result = await _caseService.CreateCaseAsync(request, userId, ct);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    private async Task<string> ExecuteGetCaseAsync(JsonElement args, string userId, CancellationToken ct)
    {
        var caseId = args.GetProperty("case_id").GetString()!;
        var result = await _caseService.GetCaseAsync(caseId, userId, ct);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    private async Task<string> ExecuteGetUserCasesAsync(JsonElement args, string userId, string entityId, CancellationToken ct)
    {
        var eid = args.TryGetProperty("entity_id", out var e) ? e.GetString() ?? entityId : entityId;
        var status = args.TryGetProperty("status", out var s) ? s.GetString() : null;
        var result = await _caseService.GetCasesAsync(userId, eid, status, ct);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    private async Task<string> ExecuteUpdateFormDataAsync(JsonElement args, string userId, CancellationToken ct)
    {
        var caseId = args.GetProperty("case_id").GetString()!;
        var formData = args.GetProperty("form_data");
        var request = new UpdateFormDataRequest(FormData: formData);
        var result = await _formService.UpdateFormDataAsync(caseId, userId, request, ct);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    private async Task<string> ExecuteValidateFormAsync(JsonElement args, string userId, CancellationToken ct)
    {
        var caseId = args.GetProperty("case_id").GetString()!;
        var formData = args.GetProperty("form_data");
        var request = new ValidateFormRequest(FormData: formData);
        var result = await _formService.ValidateFormAsync(caseId, userId, request, ct);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    private async Task<string> ExecuteSubmitFormAsync(JsonElement args, string userId, CancellationToken ct)
    {
        var caseId = args.GetProperty("case_id").GetString()!;
        var comment = args.TryGetProperty("comment", out var c) ? c.GetString() : null;
        var request = new SubmitFormRequest(Comment: comment);
        var result = await _formService.SubmitFormAsync(caseId, userId, request, ct);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    private async Task<string> ExecuteGetDocumentChecklistAsync(JsonElement args, string userId, CancellationToken ct)
    {
        var caseId = args.GetProperty("case_id").GetString()!;
        var result = await _documentService.GetDocumentChecklistAsync(caseId, userId, ct);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    private async Task<string> ExecuteGetWorkflowStateAsync(JsonElement args, string userId, CancellationToken ct)
    {
        var caseId = args.GetProperty("case_id").GetString()!;
        var result = await _workflowService.GetWorkflowStateAsync(caseId, userId, ct);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    private async Task<string> ExecuteAdvanceWorkflowStepAsync(JsonElement args, string userId, CancellationToken ct)
    {
        var caseId = args.GetProperty("case_id").GetString()!;
        var stepId = args.GetProperty("step_id").GetString()!;
        var comment = args.TryGetProperty("comment", out var c) ? c.GetString() : null;
        var request = new CompleteStepRequest(Comment: comment);
        var result = await _workflowService.CompleteStepAsync(caseId, stepId, userId, request, ct);
        return JsonSerializer.Serialize(result, JsonOptions);
    }

    // ========================================================================
    // Agent & Thread Management
    // ========================================================================

    public async Task<string> GetOrCreatePortalAgentIdAsync(CancellationToken cancellationToken = default)
    {
        var agentsClient = _clientFactory.GetAgentsClient();
        return await GetOrCreatePortalAgentAsync(agentsClient, cancellationToken);
    }

    private async Task<string> GetOrCreatePortalAgentAsync(
        PersistentAgentsClient agentsClient, CancellationToken ct)
    {
        var portalName = _aiOptions.PortalAgentName;
        if (string.IsNullOrWhiteSpace(portalName))
            throw new InvalidOperationException("AzureAI:PortalAgentName must be configured.");

        // Try to find existing portal agent
        var agents = agentsClient.Administration.GetAgentsAsync(cancellationToken: ct);
        await foreach (var agent in agents)
        {
            if (agent.Name == portalName)
                return agent.Id;
        }

        // Create a new one
        _logger.LogInformation("Creating portal agent: {AgentName}", portalName);
        var created = await agentsClient.Administration.CreateAgentAsync(
            model: _aiOptions.DefaultModelDeployment,
            name: portalName,
            instructions: _aiOptions.PortalAgentInstructions,
            cancellationToken: ct);

        return created.Value.Id;
    }

    private async Task<string> GetOrCreateThreadAsync(
        PersistentAgentsClient agentsClient, string conversationId,
        string userId, CancellationToken ct)
    {
        // Check if conversation has a thread_id stored in metadata
        var conv = await _db.GetByIdAsync<JsonElement>("conversations", conversationId, cancellationToken: ct);
        if (conv.ValueKind == JsonValueKind.Undefined)
            throw new ATMET.AI.Core.Exceptions.NotFoundException($"Conversation {conversationId} not found");

        JsonElement formData = default;
        if (conv.TryGetProperty("form_data", out formData) &&
            formData.ValueKind == JsonValueKind.Object &&
            formData.TryGetProperty("_agent_thread_id", out var threadIdEl) &&
            threadIdEl.ValueKind == JsonValueKind.String)
        {
            return threadIdEl.GetString()!;
        }

        // Create new thread
        var thread = await agentsClient.Threads.CreateThreadAsync(cancellationToken: ct);
        var threadId = thread.Value.Id;

        // Store thread ID in conversation form_data
        var existingFormData = formData.ValueKind == JsonValueKind.Object
            ? JsonSerializer.Deserialize<Dictionary<string, object?>>(formData.GetRawText()) ?? new()
            : new Dictionary<string, object?>();
        existingFormData["_agent_thread_id"] = threadId;

        await _db.UpdateAsync<JsonElement>("conversations", conversationId,
            new { form_data = existingFormData }, cancellationToken: ct);

        _logger.LogInformation("Created agent thread {ThreadId} for conversation {ConversationId}",
            threadId, conversationId);

        return threadId;
    }

    // ========================================================================
    // Message Persistence & Parsing
    // ========================================================================

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

    private async Task<List<string>> GetNewAssistantMessagesAsync(
        PersistentAgentsClient agentsClient, string threadId, CancellationToken ct)
    {
        var messages = new List<string>();

        var messagePages = agentsClient.Messages.GetMessagesAsync(
            threadId: threadId,
            order: ListSortOrder.Descending,
            cancellationToken: ct);

        await foreach (var message in messagePages)
        {
            // Stop at the first non-assistant message
            if (message.Role != MessageRole.Agent) break;

            var content = string.Join("", message.ContentItems
                .OfType<MessageTextContent>()
                .Select(c => c.Text));

            if (!string.IsNullOrEmpty(content))
                messages.Add(content);
        }

        messages.Reverse(); // chronological order
        return messages;
    }

    private static PortalChatMessage ParseAssistantMessage(string content)
    {
        // Try to parse as structured JSON first
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

        // Default: plain text message
        return new PortalChatMessage(
            Id: Guid.NewGuid().ToString(),
            Role: "assistant",
            Type: PortalMessageTypes.Text,
            Content: content,
            Data: null,
            Timestamp: DateTimeOffset.UtcNow);
    }
}
