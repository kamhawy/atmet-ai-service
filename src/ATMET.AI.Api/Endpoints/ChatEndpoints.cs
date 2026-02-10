using ATMET.AI.Core.Models.Requests;
using ATMET.AI.Core.Models.Responses;
using ATMET.AI.Core.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace ATMET.AI.Api.Endpoints;

/// <summary>
/// Endpoints for chat completions using Azure OpenAI
/// </summary>
public static class ChatEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void MapEndpoints(RouteGroupBuilder group)
    {
        var chat = group.MapGroup("/chat")
            .WithTags("Chat")
            .WithOpenApi();

        chat.MapPost("/completions", CreateCompletion)
            .WithName("CreateChatCompletion")
            .WithSummary("Create a chat completion")
            .Produces<ChatCompletionResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        chat.MapPost("/completions/stream", CreateStreamingCompletion)
            .WithName("CreateStreamingChatCompletion")
            .WithSummary("Create a streaming chat completion (SSE)")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> CreateCompletion(
        [FromBody] ChatCompletionRequest request,
        [FromServices] IChatService chatService,
        CancellationToken cancellationToken)
    {
        var completion = await chatService.CreateCompletionAsync(request, cancellationToken);
        return Results.Ok(completion);
    }

    private static async Task CreateStreamingCompletion(
        [FromBody] ChatCompletionRequest request,
        [FromServices] IChatService chatService,
        [FromServices] ILogger<Program> logger,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Connection", "keep-alive");
        context.Response.Headers.Append("X-Accel-Buffering", "no"); // Disable proxy buffering

        try
        {
            await foreach (var chunk in chatService.CreateStreamingCompletionAsync(request, cancellationToken))
            {
                var json = JsonSerializer.Serialize(chunk, JsonOptions);
                await context.Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
            }

            await context.Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — this is normal for SSE
            logger.LogInformation("Streaming chat completion cancelled by client");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during streaming chat completion");

            // If we haven't started writing, we can return an error
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsJsonAsync(new { error = "Streaming failed" }, cancellationToken);
            }
            else
            {
                // Already streaming — send an error event and terminate
                var errorEvent = JsonSerializer.Serialize(
                    new { error = "Stream interrupted" }, JsonOptions);
                await context.Response.WriteAsync($"event: error\ndata: {errorEvent}\n\n", cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
            }
        }
    }
}
