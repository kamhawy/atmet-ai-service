using ATMET.AI.Core.Exceptions;
using ATMET.AI.Core.Models.Requests;
using ATMET.AI.Core.Models.Responses;
using ATMET.AI.Core.Services;
using ATMET.AI.Infrastructure.Clients;
using ATMET.AI.Infrastructure.Configuration;
using Azure;
using Azure.AI.Agents.Persistent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ATMET.AI.Infrastructure.Services;

/// <summary>
/// Service for managing AI Agents with full Azure AI Agents.Persistent SDK integration
/// </summary>
public class AgentService : IAgentService
{
    private readonly AzureAIClientFactory _clientFactory;
    private readonly ILogger<AgentService> _logger;
    private readonly PersistentAgentsClient _agentsClient;
    private readonly IPortalAgentService _portalAgentService;
    private readonly AzureAIOptions _aiOptions;

    public AgentService(
        AzureAIClientFactory clientFactory,
        IPortalAgentService portalAgentService,
        IOptions<AzureAIOptions> aiOptions,
        ILogger<AgentService> logger)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _agentsClient = _clientFactory.GetAgentsClient();
        _portalAgentService = portalAgentService ?? throw new ArgumentNullException(nameof(portalAgentService));
        _aiOptions = aiOptions.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ====================================================================
    // Agent CRUD
    // ====================================================================

    public async Task<AgentResponse> CreateAgentAsync(
        CreateAgentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating agent with name: {AgentName}", request.Name);

            var agent = await _agentsClient.Administration.CreateAgentAsync(
                model: request.Model,
                name: request.Name,
                instructions: request.Instructions,
                tools: null,
                toolResources: null,
                temperature: request.Temperature,
                topP: request.TopP,
                responseFormat: request.ResponseFormat != null ? BinaryData.FromString(request.ResponseFormat) : null,
                metadata: request.Metadata != null ? new Dictionary<string, string>(request.Metadata) : null,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Successfully created agent: {AgentId}", agent.Value.Id);
            return MapToAgentResponse(agent.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create agent: {AgentName}", request.Name);
            throw;
        }
    }

    public async Task<List<AgentResponse>> ListAgentsAsync(
        int? limit = null,
        string? order = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Listing agents with limit: {Limit}", limit);

            var agents = new List<AgentResponse>();
            var agentPages = _agentsClient.Administration.GetAgentsAsync(cancellationToken: cancellationToken);

            await foreach (var agent in agentPages)
            {
                agents.Add(MapToAgentResponse(agent));

                if (limit.HasValue && agents.Count >= limit.Value)
                    break;
            }

            _logger.LogInformation("Retrieved {Count} agents", agents.Count);
            return agents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list agents");
            throw;
        }
    }

    public async Task<AgentResponse> GetAgentAsync(
        string agentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting agent: {AgentId}", agentId);

            var agent = await _agentsClient.Administration.GetAgentAsync(
                assistantId: agentId,
                cancellationToken);

            if (agent?.Value == null)
                throw new NotFoundException($"Agent with ID '{agentId}' not found");

            return MapToAgentResponse(agent.Value);
        }
        catch (NotFoundException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get agent: {AgentId}", agentId);
            throw;
        }
    }

    public async Task<AgentResponse> UpdateAgentAsync(
        string agentId,
        UpdateAgentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Updating agent: {AgentId}", agentId);

            var existingAgent = await _agentsClient.Administration.GetAgentAsync(
                assistantId: agentId,
                cancellationToken);

            if (existingAgent?.Value == null)
                throw new NotFoundException($"Agent with ID '{agentId}' not found");

            var updatedAgent = await _agentsClient.Administration.UpdateAgentAsync(
                assistantId: agentId,
                model: existingAgent.Value.Model,
                name: request.Name ?? existingAgent.Value.Name,
                description: request.Description ?? existingAgent.Value.Description,
                instructions: request.Instructions ?? existingAgent.Value.Instructions,
                tools: null,
                toolResources: null,
                temperature: request.Temperature ?? existingAgent.Value.Temperature,
                topP: request.TopP ?? existingAgent.Value.TopP,
                responseFormat: request.ResponseFormat != null ? BinaryData.FromString(request.ResponseFormat) : existingAgent.Value.ResponseFormat,
                metadata: request.Metadata != null ? new Dictionary<string, string>(request.Metadata) : existingAgent.Value.Metadata,
                cancellationToken);

            _logger.LogInformation("Successfully updated agent: {AgentId}", agentId);
            return MapToAgentResponse(updatedAgent.Value);
        }
        catch (NotFoundException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update agent: {AgentId}", agentId);
            throw;
        }
    }

    public async Task DeleteAgentAsync(
        string agentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Deleting agent: {AgentId}", agentId);

            await _agentsClient.Administration.DeleteAgentAsync(
                agentId: agentId,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Successfully deleted agent: {AgentId}", agentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete agent: {AgentId}", agentId);
            throw;
        }
    }

    // ====================================================================
    // Thread Operations
    // ====================================================================

    public async Task<ThreadResponse> CreateThreadAsync(
        string agentId,
        CreateThreadRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating thread for agent: {AgentId}", agentId);

            var thread = await _agentsClient.Threads.CreateThreadAsync(
                cancellationToken: cancellationToken);

            _logger.LogInformation("Successfully created thread: {ThreadId}", thread.Value.Id);

            return new ThreadResponse(
                Id: thread.Value.Id,
                CreatedAt: thread.Value.CreatedAt,
                Metadata: request?.Metadata
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create thread for agent: {AgentId}", agentId);
            throw;
        }
    }

    public async Task<ThreadResponse> GetThreadAsync(
        string threadId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting thread: {ThreadId}", threadId);

            var thread = await _agentsClient.Threads.GetThreadAsync(
                threadId: threadId,
                cancellationToken: cancellationToken);

            if (thread?.Value == null)
                throw new NotFoundException($"Thread with ID '{threadId}' not found");

            return new ThreadResponse(
                Id: thread.Value.Id,
                CreatedAt: thread.Value.CreatedAt,
                Metadata: thread.Value.Metadata?.ToDictionary(k => k.Key, v => v.Value)
            );
        }
        catch (NotFoundException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get thread: {ThreadId}", threadId);
            throw;
        }
    }

    public async Task DeleteThreadAsync(
        string threadId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Deleting thread: {ThreadId}", threadId);

            await _agentsClient.Threads.DeleteThreadAsync(
                threadId: threadId,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Successfully deleted thread: {ThreadId}", threadId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete thread: {ThreadId}", threadId);
            throw;
        }
    }

    // ====================================================================
    // Message Operations
    // ====================================================================

    public async Task<MessageResponse> AddMessageAsync(
        string threadId,
        CreateMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Adding message to thread: {ThreadId}", threadId);

            var messageRole = request.Role.ToLowerInvariant() == "user"
                ? MessageRole.User
                : MessageRole.Agent;

            var message = await _agentsClient.Messages.CreateMessageAsync(
                threadId: threadId,
                role: messageRole,
                content: request.Content,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Successfully added message: {MessageId}", message.Value.Id);

            return new MessageResponse(
                Id: message.Value.Id,
                ThreadId: threadId,
                Role: request.Role,
                Content: request.Content,
                CreatedAt: message.Value.CreatedAt,
                FileIds: request.FileIds
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add message to thread: {ThreadId}", threadId);
            throw;
        }
    }

    public async Task<List<MessageResponse>> GetMessagesAsync(
        string threadId,
        int? limit = null,
        string? order = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting messages for thread: {ThreadId}", threadId);

            var messages = new List<MessageResponse>();
            var sortOrder = order?.ToLowerInvariant() == "asc"
                ? ListSortOrder.Ascending
                : ListSortOrder.Descending;

            var messagePages = _agentsClient.Messages.GetMessagesAsync(
                threadId: threadId,
                order: sortOrder,
                cancellationToken: cancellationToken);

            await foreach (var message in messagePages)
            {
                var content = string.Join(" ", message.ContentItems
                    .OfType<MessageTextContent>()
                    .Select(c => c.Text));

                messages.Add(new MessageResponse(
                    Id: message.Id,
                    ThreadId: threadId,
                    Role: message.Role.ToString(),
                    Content: content,
                    CreatedAt: message.CreatedAt,
                    FileIds: null
                ));

                if (limit.HasValue && messages.Count >= limit.Value)
                    break;
            }

            _logger.LogInformation("Retrieved {Count} messages for thread: {ThreadId}",
                messages.Count, threadId);

            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get messages for thread: {ThreadId}", threadId);
            throw;
        }
    }

    public async Task<MessageResponse> GetMessageAsync(
        string threadId,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting message: {MessageId} from thread: {ThreadId}",
                messageId, threadId);

            var message = await _agentsClient.Messages.GetMessageAsync(
                threadId: threadId,
                messageId: messageId,
                cancellationToken: cancellationToken);

            if (message?.Value == null)
                throw new NotFoundException($"Message with ID '{messageId}' not found in thread '{threadId}'");

            var content = string.Join(" ", message.Value.ContentItems
                .OfType<MessageTextContent>()
                .Select(c => c.Text));

            return new MessageResponse(
                Id: message.Value.Id,
                ThreadId: threadId,
                Role: message.Value.Role.ToString(),
                Content: content,
                CreatedAt: message.Value.CreatedAt,
                FileIds: null
            );
        }
        catch (NotFoundException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get message: {MessageId}", messageId);
            throw;
        }
    }

    // ====================================================================
    // Run Operations
    // ====================================================================

    public async Task<RunResponse> CreateRunAsync(
        string threadId,
        CreateRunRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating run for thread: {ThreadId} with agent: {AgentId}",
                threadId, request.AgentId);

            var run = await _agentsClient.Runs.CreateRunAsync(
                threadId,
                request.AgentId,
                overrideModelName: request.OverrideModelName,
                overrideInstructions: null,
                additionalInstructions: request.Instructions,
                additionalMessages: null,
                overrideTools: null,
                stream: request.Stream,
                temperature: request.Temperature,
                topP: request.TopP,
                maxPromptTokens: request.MaxPromptTokens,
                maxCompletionTokens: request.MaxCompletionTokens,
                truncationStrategy: null,
                toolChoice: null,
                responseFormat: request.ResponseFormat != null ? BinaryData.FromString(request.ResponseFormat) : null,
                parallelToolCalls: request.ParallelToolCalls,
                metadata: request.Metadata != null ? new Dictionary<string, string>(request.Metadata) : null,
                include: null,
                cancellationToken);

            _logger.LogInformation("Successfully created run: {RunId}", run.Value.Id);
            return MapToRunResponse(run.Value, threadId, request.AgentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create run for thread: {ThreadId}", threadId);
            throw;
        }
    }

    public async Task<RunResponse> GetRunAsync(
        string threadId,
        string runId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var run = await _agentsClient.Runs.GetRunAsync(
                threadId: threadId,
                runId: runId,
                cancellationToken: cancellationToken);

            if (run?.Value == null)
                throw new NotFoundException($"Run with ID '{runId}' not found in thread '{threadId}'");

            return MapToRunResponse(run.Value, threadId, run.Value.AssistantId ?? string.Empty);
        }
        catch (NotFoundException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get run: {RunId}", runId);
            throw;
        }
    }

    public async Task<RunResponse> CancelRunAsync(
        string threadId,
        string runId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Cancelling run: {RunId} for thread: {ThreadId}", runId, threadId);

            var run = await _agentsClient.Runs.CancelRunAsync(
                threadId: threadId,
                runId: runId,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Successfully cancelled run: {RunId}", runId);
            return MapToRunResponse(run.Value, threadId, run.Value.AssistantId ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel run: {RunId}", runId);
            throw;
        }
    }

    public async Task<List<RunResponse>> ListRunsAsync(
        string threadId,
        int? limit = null,
        string? order = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Listing runs for thread: {ThreadId}", threadId);

            var runs = new List<RunResponse>();
            var sortOrder = order?.ToLowerInvariant() == "asc"
                ? ListSortOrder.Ascending
                : ListSortOrder.Descending;

            var runPages = _agentsClient.Runs.GetRunsAsync(
                threadId: threadId,
                order: sortOrder,
                cancellationToken: cancellationToken);

            await foreach (var run in runPages)
            {
                runs.Add(MapToRunResponse(run, threadId, run.AssistantId ?? string.Empty));

                if (limit.HasValue && runs.Count >= limit.Value)
                    break;
            }

            _logger.LogInformation("Retrieved {Count} runs for thread: {ThreadId}",
                runs.Count, threadId);

            return runs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list runs for thread: {ThreadId}", threadId);
            throw;
        }
    }

    // ====================================================================
    // File Operations
    // ====================================================================

    /// <summary>
    /// Adds a new document to an agent's existing vector store,
    /// making it available for the agent's file search tool
    /// on the next conversation run.
    /// </summary>
    public async Task<FileResponse> AddDocumentToAgentAsync(
        string portalAgentId,
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        // 1. Resolve agent and discover file_search vector store (GET returns tool_resources; list often does not).
        var agent = await LoadPersistentAgentForDocumentAsync(portalAgentId, cancellationToken);

        var vectorStoreId = TryGetFileSearchVectorStoreId(agent);
        if (string.IsNullOrEmpty(vectorStoreId))
        {
            _logger.LogWarning(
                "Agent {AgentName} ({AgentAssistantId}) has file_search but no vector store id in API response; creating and attaching one.",
                agent.Name,
                agent.Id);
            vectorStoreId = await EnsureVectorStoreForAgentAsync(agent, cancellationToken);
        }

        // 2. Upload the file
        var uploadedFile = await _agentsClient.Files.UploadFileAsync(
            fileStream,
            PersistentAgentFilePurpose.Agents,
            fileName,
            cancellationToken
        );

        _logger.LogInformation("Uploaded File: {FileId}", uploadedFile.Value.Id);

        // 3. Add to vector store
        var vsFile = await _agentsClient.VectorStores.CreateVectorStoreFileAsync(
            vectorStoreId, uploadedFile.Value.Id, null, null, cancellationToken);

        _logger.LogInformation("Vector Store File: {FileId}", vsFile.Value.Id);

        // 4. Wait for indexing to complete
        while (vsFile.Value.Status == VectorStoreFileStatus.InProgress)
        {
            await Task.Delay(2000, cancellationToken);
            vsFile = await _agentsClient.VectorStores.GetVectorStoreFileAsync(
                vectorStoreId, uploadedFile.Value.Id, cancellationToken);
        }

        _logger.LogInformation("Vector Store File Status: {Status}", vsFile.Value.Status);

        if (vsFile.Value.Status == VectorStoreFileStatus.Failed)
            throw new Exception($"File indexing failed: {vsFile.Value.LastError?.Message}");

        _logger.LogInformation("File Added to Vector Store: {FileId}", uploadedFile.Value.Id);

        // return the file response
        return new FileResponse(
            Id: uploadedFile.Value.Id,
            Filename: uploadedFile.Value.Filename,
            Bytes: uploadedFile.Value.Size,
            CreatedAt: uploadedFile.Value.CreatedAt,
            Purpose: uploadedFile.Value.Purpose.ToString(),
            Status: uploadedFile.Value.Status?.ToString(),
            StatusDetails: uploadedFile.Value.StatusDetails
        );
    }

    public async Task<FileResponse> UploadFileAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Uploading file: {FileName}", fileName);

            var file = await _agentsClient.Files.UploadFileAsync(
                fileStream,
                PersistentAgentFilePurpose.Agents,
                fileName,
                cancellationToken);

            _logger.LogInformation("Successfully uploaded file: {FileId}", file.Value.Id);

            // return the file response
            return new FileResponse(
                Id: file.Value.Id,
                Filename: file.Value.Filename,
                Bytes: file.Value.Size,
                CreatedAt: file.Value.CreatedAt,
                Purpose: file.Value.Purpose.ToString(),
                Status: file.Value.Status?.ToString(),
                StatusDetails: file.Value.StatusDetails
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file: {FileName}", fileName);
            throw;
        }
    }

    public async Task<FileResponse> GetFileAsync(
        string fileId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting file: {FileId}", fileId);

            var file = await _agentsClient.Files.GetFileAsync(
                fileId: fileId,
                cancellationToken: cancellationToken);

            if (file?.Value == null)
                throw new NotFoundException($"File with ID '{fileId}' not found");

            return new FileResponse(
                Id: file.Value.Id,
                Filename: file.Value.Filename,
                Bytes: file.Value.Size,
                CreatedAt: file.Value.CreatedAt,
                Purpose: file.Value.Purpose.ToString(),
                Status: file.Value.Status?.ToString(),
                StatusDetails: file.Value.StatusDetails
            );
        }
        catch (NotFoundException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file: {FileId}", fileId);
            throw;
        }
    }

    public async Task DeleteFileAsync(
        string fileId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Deleting file: {FileId}", fileId);

            await _agentsClient.Files.DeleteFileAsync(
                fileId: fileId,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Successfully deleted file: {FileId}", fileId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file: {FileId}", fileId);
            throw;
        }
    }

    /// <summary>
    /// Loads the portal agent by <see cref="AzureAIOptions.PortalAgentId"/> (<c>asst_*</c>) when set,
    /// otherwise by <see cref="AzureAIOptions.PortalAgentName"/> (Foundry display name).
    /// Uses GET so <see cref="PersistentAgent.ToolResources"/> is populated (list responses often omit it).
    /// </summary>
    private async Task<PersistentAgent> LoadPersistentAgentForDocumentAsync(
        string portalAgentId,
        CancellationToken cancellationToken)
    {
        var id = portalAgentId?.Trim() ?? string.Empty;

        if (!string.IsNullOrEmpty(id))
        {
            if (!id.StartsWith("asst_", StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "AzureAI:PortalAgentId should use the assistant id format asst_*; got {Value}. Resolving by PortalAgentName instead.",
                    id);
            }
            else
            {
                try
                {
                    var byId = await _agentsClient.Administration.GetAgentAsync(
                        assistantId: id,
                        cancellationToken: cancellationToken);
                    if (byId?.Value != null)
                        return byId.Value;
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    _logger.LogDebug(ex, "GetAgentAsync: assistant {PortalAgentId} not found; trying PortalAgentName.", id);
                }
            }
        }

        if (string.IsNullOrEmpty(_aiOptions.PortalAgentName))
        {
            throw new InvalidOperationException(
                "Configure AzureAI:PortalAgentId (asst_...) and/or AzureAI:PortalAgentName to resolve the portal agent.");
        }

        PersistentAgent? matchByName = null;
        await foreach (var item in _agentsClient.Administration.GetAgentsAsync(cancellationToken: cancellationToken))
        {
            if (string.Equals(item.Name, _aiOptions.PortalAgentName, StringComparison.Ordinal))
            {
                matchByName = item;
                break;
            }
        }

        if (matchByName == null)
        {
            throw new InvalidOperationException(
                $"No agent named '{_aiOptions.PortalAgentName}' was found in this project.");
        }

        var full = await _agentsClient.Administration.GetAgentAsync(
            assistantId: matchByName.Id,
            cancellationToken: cancellationToken);

        if (full?.Value == null)
            throw new InvalidOperationException("Agent not found.");

        _logger.LogInformation(
            "AddDocument: using agent {Name} ({AssistantId}) via PortalAgentName",
            full.Value.Name,
            full.Value.Id);

        return full.Value;
    }

    /// <summary>
    /// Reads vector store id from file_search tool resources. Foundry may use
    /// <see cref="FileSearchToolResource.VectorStoreIds"/> or enterprise <see cref="FileSearchToolResource.VectorStores"/>.
    /// </summary>
    private static string? TryGetFileSearchVectorStoreId(PersistentAgent agent)
    {
        var fileSearch = agent.ToolResources?.FileSearch;
        if (fileSearch == null)
            return null;

        var direct = fileSearch.VectorStoreIds?.FirstOrDefault(id => !string.IsNullOrEmpty(id));
        if (!string.IsNullOrEmpty(direct))
            return direct;

        if (fileSearch.VectorStores is not { Count: > 0 })
            return null;

        foreach (var entry in fileSearch.VectorStores)
        {
            var sources = entry.StoreConfiguration?.DataSources;
            if (sources is null)
                continue;

            foreach (var ds in sources)
            {
                if (string.IsNullOrEmpty(ds.AssetIdentifier))
                    continue;

                if (ds.AssetIdentifier.StartsWith("vs_", StringComparison.Ordinal))
                    return ds.AssetIdentifier;
            }
        }

        return null;
    }

    /// <summary>
    /// Creates an empty vector store, attaches it to the agent via file_search tool resources,
    /// and ensures the file_search tool is enabled.
    /// </summary>
    private async Task<string> EnsureVectorStoreForAgentAsync(
        PersistentAgent agent,
        CancellationToken cancellationToken)
    {
        var createResponse = await _agentsClient.VectorStores.CreateVectorStoreAsync(
            fileIds: Array.Empty<string>(),
            name: $"{agent.Name}-kb",
            storeConfiguration: null,
            expiresAfter: null,
            chunkingStrategy: null,
            metadata: null,
            cancellationToken: cancellationToken);

        var newVectorStoreId = createResponse.Value.Id;
        _logger.LogInformation("Created vector store {VectorStoreId} for agent {AgentId}", newVectorStoreId, agent.Id);

        var tools = agent.Tools?.ToList() ?? new List<ToolDefinition>();
        if (!tools.Any(t => t is FileSearchToolDefinition))
            tools.Add(new FileSearchToolDefinition());

        var fileSearchResource = new FileSearchToolResource(
            new List<string> { newVectorStoreId },
            vectorStores: null);

        var mergedResources = new ToolResources
        {
            CodeInterpreter = agent.ToolResources?.CodeInterpreter,
            FileSearch = fileSearchResource,
            AzureAISearch = agent.ToolResources?.AzureAISearch
        };

        if (agent.ToolResources?.Mcp is { Count: > 0 })
        {
            _logger.LogWarning(
                "Agent {AgentId} has MCP tool resources; this update cannot set MCP on ToolResources in the current SDK and the service may drop MCP unless the API merges server-side.",
                agent.Id);
        }

        await _agentsClient.Administration.UpdateAgentAsync(
            assistantId: agent.Id,
            model: agent.Model,
            name: agent.Name,
            description: agent.Description,
            instructions: agent.Instructions,
            tools: tools,
            toolResources: mergedResources,
            temperature: agent.Temperature,
            topP: agent.TopP,
            responseFormat: agent.ResponseFormat,
            metadata: agent.Metadata,
            cancellationToken: cancellationToken);

        return newVectorStoreId;
    }

    // ====================================================================
    // Private Mappers
    // ====================================================================

    private static AgentResponse MapToAgentResponse(PersistentAgent agent)
    {
        var toolTypes = agent.Tools?
            .Select(t => t.GetType().Name.Replace("Definition", ""))
            .ToList();

        return new AgentResponse(
            Id: agent.Id,
            Name: agent.Name,
            Model: agent.Model,
            Instructions: agent.Instructions,
            CreatedAt: agent.CreatedAt,
            Metadata: agent.Metadata?.ToDictionary(k => k.Key, v => v.Value),
            Description: agent.Description,
            Temperature: agent.Temperature,
            TopP: agent.TopP,
            ResponseFormat: agent.ResponseFormat?.ToString(),
            ToolTypes: toolTypes,
            ToolResources: null
        );
    }

    private static RunResponse MapToRunResponse(ThreadRun run, string threadId, string agentId)
    {
        RunUsage? usage = null;
        if (run.Usage != null)
        {
            usage = new RunUsage(
                PromptTokens: run.Usage.PromptTokens,
                CompletionTokens: run.Usage.CompletionTokens,
                TotalTokens: run.Usage.TotalTokens
            );
        }

        return new RunResponse(
            Id: run.Id,
            ThreadId: run.ThreadId ?? threadId,
            AgentId: agentId,
            Status: run.Status.ToString(),
            CreatedAt: run.CreatedAt,
            CompletedAt: run.CompletedAt,
            LastError: run.LastError?.Message,
            AssistantId: run.AssistantId,
            Model: run.Model,
            Instructions: run.Instructions,
            StartedAt: run.StartedAt,
            ExpiresAt: run.ExpiresAt,
            CancelledAt: run.CancelledAt,
            FailedAt: run.FailedAt,
            LastErrorCode: run.LastError?.Code,
            Usage: usage,
            MaxPromptTokens: run.MaxPromptTokens,
            MaxCompletionTokens: run.MaxCompletionTokens,
            ParallelToolCalls: run.ParallelToolCalls,
            Temperature: run.Temperature,
            TopP: run.TopP,
            Metadata: run.Metadata?.ToDictionary(k => k.Key, v => v.Value),
            IncompleteReason: run.IncompleteDetails?.Reason.ToString()
        );
    }
}
