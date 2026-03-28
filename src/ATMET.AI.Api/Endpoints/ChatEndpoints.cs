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
            .WithTags("Chat");

        chat.MapPost("/completions", CreateCompletion)
            .WithName("CreateChatCompletion")
            .WithSummary("Create a chat completion")
            .WithDescription("""
                **Stateless** Azure OpenAI chat completion. **`messages`** follow the usual chat roles (`system`, `user`, `assistant`).

                **`model`** is optional: when omitted, the API uses the default deployment configured for this host.

                **Tuning:** `temperature`, `maxTokens`, `topP`, `stopSequences` map to the underlying OpenAI options.

                **Response:** `ChatCompletionResponse` with **`choices`**, **`usage`** token counts, and **`model`** echo.
                """)
            .Produces<ChatCompletionResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        chat.MapPost("/completions/stream", CreateStreamingCompletion)
            .WithName("CreateStreamingChatCompletion")
            .WithSummary("Create a streaming chat completion (SSE)")
            .WithDescription("""
                Same request body as **`/completions`**, but the HTTP response is **`text/event-stream`**.

                **Wire format:** repeated lines `data: {json}` where JSON is a **`ChatCompletionChunk`** (delta content in `choices[].delta.content`). The stream always ends with **`data: [DONE]`**.

                **Clients:** use `EventSource` or manual `fetch` + ReadableStream; disable proxy buffering (`X-Accel-Buffering: no` is set on the response).
                """)
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
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
        [FromServices] ILoggerFactory loggerFactory,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Connection", "keep-alive");
        context.Response.Headers.Append("X-Accel-Buffering", "no"); // Disable proxy buffering

        var logger = loggerFactory.CreateLogger("ATMET.AI.Api.Endpoints.Chat");
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
