using ATMET.AI.Core.Models.Requests;

namespace ATMET.AI.Core.Models.Responses
{
    #region Agent (PersistentAgent SDK)

    /// <summary>
    /// Response model aligned with Azure.AI.Agents.Persistent.PersistentAgent.
    /// </summary>
    public record AgentResponse(
        string Id,
        string Name,
        string Model,
        string? Instructions,
        DateTimeOffset CreatedAt,
        Dictionary<string, string>? Metadata,
        string? Description = null,
        float? Temperature = null,
        float? TopP = null,
        string? ResponseFormat = null,
        List<string>? ToolTypes = null,
        Dictionary<string, object>? ToolResources = null
    );

    #endregion

    #region Thread (AgentThread SDK)

    /// <summary>
    /// Response model aligned with Azure.AI.Agents.Persistent AgentThread.
    /// </summary>
    public record ThreadResponse(
        string Id,
        DateTimeOffset CreatedAt,
        Dictionary<string, string>? Metadata,
        string? Object = null
    );

    #endregion

    #region Message (ThreadMessage SDK)

    /// <summary>
    /// Response model aligned with Azure.AI.Agents.Persistent ThreadMessage.
    /// </summary>
    public record MessageResponse(
        string Id,
        string ThreadId,
        string Role,
        string Content,
        DateTimeOffset CreatedAt,
        List<string>? FileIds,
        List<MessageContentItem>? ContentItems = null,
        string? Object = null
    );

    /// <summary>
    /// Represents a content item in a message (text, file reference, etc.).
    /// </summary>
    public record MessageContentItem(
        string Type,
        string? Text = null,
        string? FileId = null
    );

    #endregion

    #region Run (ThreadRun SDK)

    /// <summary>
    /// Response model aligned with Azure.AI.Agents.Persistent.ThreadRun.
    /// </summary>
    public record RunResponse(
        string Id,
        string ThreadId,
        string AgentId,
        string Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset? CompletedAt,
        string? LastError,
        string? AssistantId = null,
        string? Model = null,
        string? Instructions = null,
        DateTimeOffset? StartedAt = null,
        DateTimeOffset? ExpiresAt = null,
        DateTimeOffset? CancelledAt = null,
        DateTimeOffset? FailedAt = null,
        string? LastErrorCode = null,
        RunUsage? Usage = null,
        int? MaxPromptTokens = null,
        int? MaxCompletionTokens = null,
        bool? ParallelToolCalls = null,
        float? Temperature = null,
        float? TopP = null,
        Dictionary<string, string>? Metadata = null,
        string? IncompleteReason = null
    );

    /// <summary>
    /// Usage statistics for a run, aligned with RunCompletionUsage.
    /// </summary>
    public record RunUsage(
        long PromptTokens,
        long CompletionTokens,
        long TotalTokens
    );

    #endregion

    #region File (PersistentAgentFileInfo SDK)

    /// <summary>
    /// Response model aligned with Azure.AI.Agents.Persistent.PersistentAgentFileInfo.
    /// </summary>
    public record FileResponse(
        string Id,
        string Filename,
        long Bytes,
        DateTimeOffset CreatedAt,
        string? Purpose = null,
        string? Status = null,
        string? StatusDetails = null
    );

    #endregion

    #region Deployment (ModelDeployment / AIProjectDeployment SDK)

    /// <summary>
    /// Response model aligned with Azure.AI.Projects.ModelDeployment and AIProjectDeployment.
    /// </summary>
    public record DeploymentResponse(
        string Name,
        string Model,
        string Publisher,
        string Type,
        string Status,
        string? ModelVersion = null,
        string? ConnectionName = null,
        string? Sku = null,
        Dictionary<string, object>? Capabilities = null
    );

    #endregion

    #region Connection (AIProjectConnection SDK)

    /// <summary>
    /// Response model aligned with Azure.AI.Projects.AIProjectConnection.
    /// </summary>
    public record ConnectionResponse(
        string Name,
        string Type,
        string? Target,
        Dictionary<string, object>? Properties,
        string? Id = null,
        bool? IsDefault = null,
        string? Category = null
    );

    #endregion

    #region Dataset (AIProjectDataset SDK)

    /// <summary>
    /// Response model aligned with Azure.AI.Projects.AIProjectDataset.
    /// </summary>
    public record DatasetResponse(
        string Id,
        string Name,
        string Version,
        string Type,
        DateTimeOffset CreatedAt,
        string? Description = null,
        string? ConnectionName = null,
        bool? IsReference = null,
        string? DataUri = null,
        Dictionary<string, string>? Tags = null
    );

    /// <summary>
    /// Response model aligned with Azure.AI.Projects.DatasetCredential and AIProjectBlobReference.
    /// </summary>
    public record DatasetCredentialsResponse(
        string SasUri,
        DateTimeOffset ExpiresAt,
        string? StorageAccountArmId = null,
        string? BlobUri = null,
        string? CredentialType = null
    );

    #endregion

    #region Index (AIProjectIndex / AzureAISearchIndex SDK)

    /// <summary>
    /// Response model aligned with Azure.AI.Projects.AIProjectIndex and AzureAISearchIndex.
    /// </summary>
    public record IndexResponse(
        string Id,
        string Name,
        string Version,
        string ConnectionName,
        string IndexName,
        string? Description,
        DateTimeOffset CreatedAt,
        string? IndexType = null,
        Dictionary<string, string>? Tags = null,
        object? FieldMapping = null
    );

    #endregion

    #region Chat Completion (Azure.AI.OpenAI ChatCompletion SDK)

    /// <summary>
    /// Response model aligned with Azure.AI.OpenAI ChatCompletion.
    /// </summary>
    public record ChatCompletionResponse(
        string Id,
        List<ChatChoice> Choices,
        Usage Usage,
        DateTimeOffset Created,
        string? Model = null,
        string? SystemFingerprint = null
    );

    public record ChatChoice(
        int Index,
        ChatMessage Message,
        string FinishReason,
        int? LogProbsIndex = null
    );

    public record Usage(
        int PromptTokens,
        int CompletionTokens,
        int TotalTokens
    );

    /// <summary>
    /// Streaming chunk aligned with StreamingChatCompletionUpdate.
    /// </summary>
    public record ChatCompletionChunk(
        string Id,
        List<ChatChoiceDelta> Choices,
        DateTimeOffset Created,
        string? Model = null
    );

    public record ChatChoiceDelta(
        int Index,
        string? Content,
        string? FinishReason
    );

    #endregion
}
