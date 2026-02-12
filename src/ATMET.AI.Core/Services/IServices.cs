using ATMET.AI.Core.Models.Requests;
using ATMET.AI.Core.Models.Responses;

namespace ATMET.AI.Core.Services;

/// <summary>
/// Service for managing AI Agents
/// </summary>
public interface IAgentService
{
    Task<AgentResponse> CreateAgentAsync(CreateAgentRequest request, CancellationToken cancellationToken = default);
    Task<List<AgentResponse>> ListAgentsAsync(int? limit = null, string? order = null, CancellationToken cancellationToken = default);
    Task<AgentResponse> GetAgentAsync(string agentId, CancellationToken cancellationToken = default);
    Task<AgentResponse> UpdateAgentAsync(string agentId, UpdateAgentRequest request, CancellationToken cancellationToken = default);
    Task DeleteAgentAsync(string agentId, CancellationToken cancellationToken = default);

    // Thread operations
    Task<ThreadResponse> CreateThreadAsync(string agentId, CreateThreadRequest? request = null, CancellationToken cancellationToken = default);
    Task<ThreadResponse> GetThreadAsync(string threadId, CancellationToken cancellationToken = default);
    Task DeleteThreadAsync(string threadId, CancellationToken cancellationToken = default);

    // Message operations
    Task<MessageResponse> AddMessageAsync(string threadId, CreateMessageRequest request, CancellationToken cancellationToken = default);
    Task<List<MessageResponse>> GetMessagesAsync(string threadId, int? limit = null, string? order = null, CancellationToken cancellationToken = default);
    Task<MessageResponse> GetMessageAsync(string threadId, string messageId, CancellationToken cancellationToken = default);

    // Run operations
    Task<RunResponse> CreateRunAsync(string threadId, CreateRunRequest request, CancellationToken cancellationToken = default);
    Task<RunResponse> GetRunAsync(string threadId, string runId, CancellationToken cancellationToken = default);
    Task<RunResponse> CancelRunAsync(string threadId, string runId, CancellationToken cancellationToken = default);
    Task<List<RunResponse>> ListRunsAsync(string threadId, int? limit = null, string? order = null, CancellationToken cancellationToken = default);

    // File operations — use Stream to avoid ASP.NET dependency in Core layer
    Task<FileResponse> UploadFileAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);
    Task<FileResponse> GetFileAsync(string fileId, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string fileId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for managing AI model deployments
/// </summary>
public interface IDeploymentService
{
    Task<List<DeploymentResponse>> ListDeploymentsAsync(string? modelPublisher = null, string? modelType = null, CancellationToken cancellationToken = default);
    Task<DeploymentResponse> GetDeploymentAsync(string deploymentName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for managing Azure resource connections
/// </summary>
public interface IConnectionService
{
    Task<List<ConnectionResponse>> ListConnectionsAsync(string? connectionType = null, CancellationToken cancellationToken = default);
    Task<ConnectionResponse> GetConnectionAsync(string connectionName, CancellationToken cancellationToken = default);
    Task<ConnectionResponse> GetDefaultConnectionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for managing datasets
/// </summary>
public interface IDatasetService
{
    Task<DatasetResponse> UploadFileAsync(string name, string version, Stream fileStream, string fileName, string connectionName, CancellationToken cancellationToken = default);
    Task<DatasetResponse> UploadFolderAsync(string name, string version, IEnumerable<(Stream Stream, string FileName)> files, string connectionName, string? filePattern = null, CancellationToken cancellationToken = default);
    Task<List<DatasetResponse>> ListDatasetsAsync(CancellationToken cancellationToken = default);
    Task<List<DatasetResponse>> ListDatasetVersionsAsync(string name, CancellationToken cancellationToken = default);
    Task<DatasetResponse> GetDatasetAsync(string name, string version, CancellationToken cancellationToken = default);
    Task<DatasetCredentialsResponse> GetCredentialsAsync(string name, string version, CancellationToken cancellationToken = default);
    Task DeleteDatasetAsync(string name, string version, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for managing search indexes
/// </summary>
public interface IIndexService
{
    Task<IndexResponse> CreateOrUpdateIndexAsync(string name, string version, string connectionName, string indexName, string? description = null, CancellationToken cancellationToken = default);
    Task<List<IndexResponse>> ListIndexesAsync(CancellationToken cancellationToken = default);
    Task<List<IndexResponse>> ListIndexVersionsAsync(string name, CancellationToken cancellationToken = default);
    Task<IndexResponse> GetIndexAsync(string name, string version, CancellationToken cancellationToken = default);
    Task DeleteIndexAsync(string name, string version, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for chat completions
/// </summary>
public interface IChatService
{
    Task<ChatCompletionResponse> CreateCompletionAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ChatCompletionChunk> CreateStreamingCompletionAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default);
}
