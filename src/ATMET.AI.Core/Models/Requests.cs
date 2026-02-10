namespace ATMET.AI.Core.Models.Requests
{
    public record CreateAgentRequest(
        string Model,
        string Name,
        string? Instructions = null,
        Dictionary<string, string>? Metadata = null,
        List<object>? Tools = null
    );

    public record UpdateAgentRequest(
        string? Name = null,
        string? Instructions = null,
        Dictionary<string, string>? Metadata = null,
        List<object>? Tools = null
    );

    public record CreateThreadRequest(
        Dictionary<string, string>? Metadata = null
    );

    public record CreateMessageRequest(
        string Role,
        string Content,
        List<string>? FileIds = null
    );

    public record CreateRunRequest(
        string AgentId,
        string? Instructions = null,
        Dictionary<string, string>? Metadata = null
    );

    public record CreateDatasetRequest(
        string Name,
        string Version,
        string ConnectionName,
        string? FilePattern = null
    );

    public record CreateIndexRequest(
        string Name,
        string Version,
        string ConnectionName,
        string IndexName,
        string? Description = null
    );

    public record ChatCompletionRequest(
        string Model,
        List<ChatMessage> Messages,
        double? Temperature = null,
        int? MaxTokens = null,
        bool? Stream = null
    );

    public record ChatMessage(
        string Role,
        string Content
    );
}
