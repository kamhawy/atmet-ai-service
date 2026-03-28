namespace ATMET.AI.Core.Models.Requests
{
    /// <summary>
    /// Creates a **persistent agent** in the configured Azure AI Foundry project (Azure AI Agents Persistent API shape).
    /// </summary>
    /// <param name="Model">Deployment name in the project (for example <c>gpt-4o</c>) the agent should run on.</param>
    /// <param name="Name">Human-readable agent label for operators.</param>
    /// <param name="Instructions">System / developer instructions (behavior, tone, tool usage hints).</param>
    /// <param name="Metadata">Arbitrary string key-value metadata stored with the agent.</param>
    /// <param name="Tools">Tool definitions (code interpreter, file search, function tools) as SDK-shaped objects.</param>
    /// <param name="Description">Optional longer description for catalogs or admin UIs.</param>
    /// <param name="Temperature">Sampling temperature for generations (model-dependent).</param>
    /// <param name="TopP">Nucleus sampling (<c>top_p</c>).</param>
    /// <param name="ResponseFormat">Optional response format constraint (for example JSON mode) when supported.</param>
    /// <param name="ToolResources">Additional tool configuration (vector store ids, etc.).</param>
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
    /// **Patch-style** update for an existing persistent agent (omit fields you do not want to change).
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
    /// Adds a **user**, **assistant**, or **system** message to a thread.
    /// </summary>
    /// <param name="Role">One of <c>user</c>, <c>assistant</c>, or <c>system</c>.</param>
    /// <param name="Content">Primary text content when not using structured <paramref name="ContentItems"/>.</param>
    /// <param name="FileIds">Attached files previously uploaded via <c>POST /api/v1/agents/files</c>.</param>
    /// <param name="ContentItems">Multi-part content blocks (text + file references) for rich messages.</param>
    public record CreateMessageRequest(
        string Role,
        string Content,
        List<string>? FileIds = null,
        List<MessageContentItemRequest>? ContentItems = null
    );

    /// <summary>
    /// One **content part** inside a multi-part agent message.
    /// </summary>
    /// <param name="Type">Discriminator, for example <c>text</c> or <c>image_file</c> per SDK conventions.</param>
    /// <param name="Text">Plain text when <paramref name="Type"/> is textual.</param>
    /// <param name="FileId">Reference to an uploaded agent file when applicable.</param>
    public record MessageContentItemRequest(
        string Type,
        string? Text = null,
        string? FileId = null
    );

    /// <summary>
    /// Starts a **run**: the agent executes over the current thread messages until a terminal run status.
    /// </summary>
    /// <param name="AgentId">Agent that should execute (must belong to the same project).</param>
    /// <param name="Instructions">Optional per-run system instructions overriding defaults.</param>
    /// <param name="Metadata">Run-scoped metadata for tracing.</param>
    /// <param name="Stream">Forwarded to the Azure Agents SDK where supported (this REST endpoint returns a materialized <c>RunResponse</c>).</param>
    /// <param name="Temperature">Per-run temperature override.</param>
    /// <param name="TopP">Per-run top-p override.</param>
    /// <param name="MaxPromptTokens">Cap on prompt tokens when supported.</param>
    /// <param name="MaxCompletionTokens">Cap on completion tokens when supported.</param>
    /// <param name="ParallelToolCalls">Whether parallel tool calls are allowed.</param>
    /// <param name="OverrideModelName">Temporary model deployment override for this run.</param>
    /// <param name="ResponseFormat">Response format hint for the completion leg of the run.</param>
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
    /// **Chat completion** request (Azure OpenAI shape) for <c>POST /api/v1/chat/completions</c>.
    /// </summary>
    /// <param name="Model">Optional deployment name; server default is used when null.</param>
    /// <param name="Messages">Ordered transcript including optional system priming.</param>
    /// <param name="Temperature">Sampling temperature.</param>
    /// <param name="MaxTokens">Upper bound on tokens to generate (legacy name; maps to service max completion tokens).</param>
    /// <param name="Stream">Ignored for the non-streaming route; present for payload symmetry.</param>
    /// <param name="TopP">Nucleus sampling.</param>
    /// <param name="StopSequences">Optional stop sequences that truncate generation.</param>
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
    /// One **role/content** pair inside <see cref="ChatCompletionRequest.Messages"/>.
    /// </summary>
    public record ChatMessage(
        string Role,
        string Content,
        string? Name = null
    );
}
