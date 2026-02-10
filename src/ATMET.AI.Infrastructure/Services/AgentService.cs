using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using ATMET.AI.Core.Exceptions;
using ATMET.AI.Core.Models.Requests;
using ATMET.AI.Core.Models.Responses;
using ATMET.AI.Core.Services;
using ATMET.AI.Infrastructure.Clients;
using Microsoft.Extensions.Logging;

namespace ATMET.AI.Infrastructure.Services;

/// <summary>
/// Service for managing AI Agents with full Azure AI Agents.Persistent SDK integration
/// </summary>
public class AgentService : IAgentService
{
    private readonly AzureAIClientFactory _clientFactory;
    private readonly ILogger<AgentService> _logger;
    private readonly PersistentAgentsClient _agentsClient;

    public AgentService(
        AzureAIClientFactory clientFactory,
        ILogger<AgentService> logger)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _agentsClient = _clientFactory.GetAgentsClient();
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
                description: null,
                instructions: request.Instructions ?? existingAgent.Value.Instructions,
                tools: null,
                toolResources: null,
                temperature: null,
                topP: null,
                responseFormat: null,
                metadata: null,
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
                overrideModelName: null,
                overrideInstructions: null,
                additionalInstructions: request.Instructions,
                additionalMessages: null,
                overrideTools: null,
                stream: null,
                temperature: null,
                topP: null,
                maxPromptTokens: null,
                maxCompletionTokens: null,
                truncationStrategy: null,
                toolChoice: null,
                responseFormat: null,
                parallelToolCalls: null,
                metadata: null,
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

            return new FileResponse(
                Id: file.Value.Id,
                Filename: file.Value.Filename,
                Bytes: file.Value.Size,
                CreatedAt: file.Value.CreatedAt
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
                CreatedAt: file.Value.CreatedAt
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

    // ====================================================================
    // Private Mappers
    // ====================================================================

    private static AgentResponse MapToAgentResponse(PersistentAgent agent)
    {
        return new AgentResponse(
            Id: agent.Id,
            Name: agent.Name,
            Model: agent.Model,
            Instructions: agent.Instructions,
            CreatedAt: agent.CreatedAt,
            Metadata: agent.Metadata?.ToDictionary(k => k.Key, v => v.Value)
        );
    }

    private static RunResponse MapToRunResponse(ThreadRun run, string threadId, string agentId)
    {
        return new RunResponse(
            Id: run.Id,
            ThreadId: threadId,
            AgentId: agentId,
            Status: run.Status.ToString(),
            CreatedAt: run.CreatedAt,
            CompletedAt: run.CompletedAt,
            LastError: run.LastError?.Message
        );
    }
}
