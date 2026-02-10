using ATMET.AI.Core.Models.Requests;

namespace ATMET.AI.Core.Models.Responses
{
    public record AgentResponse(
        string Id,
        string Name,
        string Model,
        string? Instructions,
        DateTimeOffset CreatedAt,
        Dictionary<string, string>? Metadata
    );

    public record ThreadResponse(
        string Id,
        DateTimeOffset CreatedAt,
        Dictionary<string, string>? Metadata
    );

    public record MessageResponse(
        string Id,
        string ThreadId,
        string Role,
        string Content,
        DateTimeOffset CreatedAt,
        List<string>? FileIds
    );

    public record RunResponse(
        string Id,
        string ThreadId,
        string AgentId,
        string Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset? CompletedAt,
        string? LastError
    );

    public record FileResponse(
        string Id,
        string Filename,
        long Bytes,
        DateTimeOffset CreatedAt
    );

    public record DeploymentResponse(
        string Name,
        string Model,
        string Publisher,
        string Type,
        string Status
    );

    public record ConnectionResponse(
        string Name,
        string Type,
        string Category,
        string? Target,
        Dictionary<string, object>? Properties
    );

    public record DatasetResponse(
        string Id,
        string Name,
        string Version,
        string Type,
        DateTimeOffset CreatedAt
    );

    public record DatasetCredentialsResponse(
        string SasUri,
        DateTimeOffset ExpiresAt
    );

    public record IndexResponse(
        string Id,
        string Name,
        string Version,
        string ConnectionName,
        string IndexName,
        string? Description,
        DateTimeOffset CreatedAt
    );

    public record ChatCompletionResponse(
        string Id,
        List<ChatChoice> Choices,
        Usage Usage,
        DateTimeOffset Created
    );

    public record ChatChoice(
        int Index,
        ChatMessage Message,
        string FinishReason
    );

    public record Usage(
        int PromptTokens,
        int CompletionTokens,
        int TotalTokens
    );

    public record ChatCompletionChunk(
        string Id,
        List<ChatChoiceDelta> Choices,
        DateTimeOffset Created
    );

    public record ChatChoiceDelta(
        int Index,
        string? Content,
        string? FinishReason
    );
}
