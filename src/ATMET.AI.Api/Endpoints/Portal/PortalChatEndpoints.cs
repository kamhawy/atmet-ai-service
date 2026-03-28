using ATMET.AI.Core.Models.Portal;
using ATMET.AI.Core.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ATMET.AI.Api.Endpoints.Portal;

/// <summary>
/// SSE streaming endpoint for portal AI agent chat.
/// All portal mutations flow through this endpoint — the agent
/// calls tools (create_case, submit_form, etc.) and streams
/// structured responses to the frontend.
/// </summary>
public static class PortalChatEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static void MapEndpoints(RouteGroupBuilder group)
    {
        var chat = group.MapGroup("/portal/conversations/{conversationId}/chat")
            .WithTags("Portal Chat");

        chat.MapPost("", ProcessMessage)
            .WithName("PortalChat")
            .WithSummary("Send a message and stream AI agent response (SSE)")
            .WithDescription(
                "Sends a user message to the portal AI agent. The agent processes the message, " +
                "optionally calls tools (create case, update form, etc.), and streams structured " +
                "responses as Server-Sent Events. Event types: typing, tool_call, message, done, error.")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task ProcessMessage(
        string conversationId,
        [FromBody] PortalChatMessage userMessage,
        [FromHeader(Name = "X-Portal-User-Id")] string userId,
        [FromHeader(Name = "X-Portal-Entity-Id")] string entityId,
        [FromHeader(Name = "X-Portal-Language")] string? language,
        [FromServices] IPortalAgentService agentService,
        [FromServices] ILoggerFactory loggerFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Connection", "keep-alive");
        context.Response.Headers.Append("X-Accel-Buffering", "no");

        var logger = loggerFactory.CreateLogger("ATMET.AI.Api.Endpoints.PortalChat");

        try
        {
            await foreach (var evt in agentService.ProcessMessageAsync(
                conversationId, userId, entityId, userMessage, language ?? "en", cancellationToken))
            {
                var json = JsonSerializer.Serialize(evt, JsonOptions);
                await context.Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
            }

            await context.Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Portal chat stream cancelled by client for conversation {ConversationId}", conversationId);
        }
        catch (ATMET.AI.Core.Exceptions.NotFoundException ex)
        {
            logger.LogWarning(ex, "Conversation {ConversationId} not found", conversationId);

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsJsonAsync(
                    new { error = ex.Message }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during portal chat for conversation {ConversationId}", conversationId);

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(
                    new { error = "Chat processing failed" }, cancellationToken);
            }
            else
            {
                var errorEvent = new PortalChatEvent(
                    EventType: PortalChatEventTypes.Error,
                    Message: new PortalChatMessage(
                        Id: Guid.NewGuid().ToString(),
                        Role: "system",
                        Type: PortalMessageTypes.Error,
                        Content: null,
                        Data: JsonSerializer.SerializeToElement(new ChatErrorData(
                            Code: "stream_error",
                            Message: "An error occurred while processing your request.",
                            Retryable: true
                        ), JsonOptions),
                        Timestamp: DateTimeOffset.UtcNow),
                    ToolName: null,
                    ToolStatus: null);

                var errorJson = JsonSerializer.Serialize(errorEvent, JsonOptions);
                await context.Response.WriteAsync($"data: {errorJson}\n\n", cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
            }
        }
    }
}
