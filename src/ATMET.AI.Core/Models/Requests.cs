namespace ATMET.AI.Core.Models.Requests
{
    /// <summary>
    /// Request model aligned with Azure.AI.Agents.Persistent CreateAgent parameters.
    /// </summary>
    public record CreateAgentRequest(
        string Model,
        string Name,
        string? Instructions = null,
        Dictionary<string, string>? Metadata = null,
        List<object>? Tools = null,
        string? Description = null,
        float? Temperature = null,
        float? TopP = null,
        string? ResponseFormat = null,
        Dictionary<string, object>? ToolResources = null
    );

    /// <summary>
    /// Request model aligned with Azure.AI.Agents.Persistent UpdateAgent parameters.
    /// </summary>
    public record UpdateAgentRequest(
        string? Name = null,
        string? Instructions = null,
        Dictionary<string, string>? Metadata = null,
        List<object>? Tools = null,
        string? Description = null,
        float? Temperature = null,
        float? TopP = null,
        string? ResponseFormat = null,
        Dictionary<string, object>? ToolResources = null
    );

    /// <summary>
    /// Request model aligned with Azure.AI.Agents.Persistent CreateThread parameters.
    /// </summary>
    public record CreateThreadRequest(
        Dictionary<string, string>? Metadata = null
    );

    /// <summary>
    /// Request model aligned with Azure.AI.Agents.Persistent CreateMessage parameters.
    /// </summary>
    public record CreateMessageRequest(
        string Role,
        string Content,
        List<string>? FileIds = null,
        List<MessageContentItemRequest>? ContentItems = null
    );

    /// <summary>
    /// Content item for multi-part messages (text, file reference).
    /// </summary>
    public record MessageContentItemRequest(
        string Type,
        string? Text = null,
        string? FileId = null
    );

    /// <summary>
    /// Request model aligned with Azure.AI.Agents.Persistent CreateRun parameters.
    /// </summary>
    public record CreateRunRequest(
        string AgentId,
        string? Instructions = null,
        Dictionary<string, string>? Metadata = null,
        bool? Stream = null,
        float? Temperature = null,
        float? TopP = null,
        int? MaxPromptTokens = null,
        int? MaxCompletionTokens = null,
        bool? ParallelToolCalls = null,
        string? OverrideModelName = null,
        string? ResponseFormat = null
    );

    /// <summary>
    /// Request model aligned with Azure.AI.Projects dataset upload/create parameters.
    /// </summary>
    public record CreateDatasetRequest(
        string Name,
        string Version,
        string ConnectionName,
        string? FilePattern = null,
        string? Description = null,
        bool? IsReference = null,
        Dictionary<string, string>? Tags = null
    );

    /// <summary>
    /// Request model aligned with Azure.AI.Projects AzureAISearchIndex parameters.
    /// </summary>
    public record CreateIndexRequest(
        string Name,
        string Version,
        string ConnectionName,
        string IndexName,
        string? Description = null,
        Dictionary<string, string>? Tags = null,
        object? FieldMapping = null
    );

    /// <summary>
    /// Request model aligned with Azure.AI.OpenAI ChatCompletion parameters.
    /// </summary>
    public record ChatCompletionRequest(
        string? Model,
        List<ChatMessage> Messages,
        double? Temperature = null,
        int? MaxTokens = null,
        bool? Stream = null,
        double? TopP = null,
        IReadOnlyList<string>? StopSequences = null
    );

    /// <summary>
    /// Chat message for completion request.
    /// </summary>
    public record ChatMessage(
        string Role,
        string Content,
        string? Name = null
    );
}
