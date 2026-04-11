using ATMET.AI.Core.Models.Portal;
using ATMET.AI.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ATMET.AI.Api.Endpoints.Portal;

/// <summary>
/// Portal conversation threads (persisted chat) for the citizen UI and AI agent.
/// </summary>
public static class PortalConversationsEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        var conversations = group.MapGroup("/portal/conversations")
            .WithTags("Portal - Conversations");

        conversations.MapGet("/", GetConversations)
            .WithName("GetPortalConversations")
            .WithSummary("List conversations for the authenticated user")
            .WithDescription("""
                Returns **conversation summaries** for the user within the entity: title, linked case/service, last message preview, counts, timestamps.

                **Business use:** chat history sidebar before opening **Portal Chat (SSE)**.

                **Headers:** `X-Portal-User-Id` and `X-Portal-Entity-Id` (required).
                """)
            .Produces<List<PortalConversationSummaryResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization("ApiReader");

        conversations.MapPost("/", CreateConversation)
            .WithName("CreatePortalConversation")
            .WithSummary("Create a new conversation")
            .WithDescription("""
                Starts a **new thread**. Body (`CreateConversationRequest`): **`entityId`**, optional **`caseId`**, **`serviceId`**, **`title`**.

                **Business use:** open a fresh AI-assisted session or attach chat to an existing case.

                **Headers:** `X-Portal-User-Id` (required).
                """)
            .Produces<PortalConversationResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization("ApiWriter");

        conversations.MapGet("/{conversationId}", GetConversation)
            .WithName("GetPortalConversation")
            .WithSummary("Get conversation with all messages")
            .WithDescription("""
                Loads the **full thread** including **`messages`** (role, content, optional structured attachments) and persisted **`formData`** snapshot when present.

                When a Foundry workflow session exists, the response also includes optional **Foundry** fields (e.g. `foundryProjectConversationId`, `lastResponseId`, `pauseUiAction`, `foundryCurrentStep`, `conversationLanguage`) for resume/rehydration.

                **Headers:** `X-Portal-User-Id` (required). **`404`** if not found or not owned.
                """)
            .Produces<PortalConversationResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization("ApiReader");

        conversations.MapDelete("/{conversationId}", DeleteConversation)
            .WithName("DeletePortalConversation")
            .WithSummary("Delete a conversation")
            .WithDescription("""
                Permanently removes the conversation for this user.

                **Headers:** `X-Portal-User-Id` (required). **`204 No Content`** on success.
                """)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization("ApiWriter");

        conversations.MapPost("/{conversationId}/messages", SendMessage)
            .WithName("SendPortalMessage")
            .WithSummary("Send a message in a conversation")
            .WithDescription("""
                Appends a **non-streaming** message (for example operator messages or simple chat). Body: **`content`**, optional **`type`**.

                For **AI agent streaming**, prefer **`POST /portal/conversations/{conversationId}/chat`** (SSE).

                **Headers:** `X-Portal-User-Id` (required).
                """)
            .Produces<PortalMessageResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization("ApiWriter");
    }

    private static async Task<IResult> GetConversations(
        [FromHeader(Name = "X-Portal-User-Id")] string userId,
        [FromHeader(Name = "X-Portal-Entity-Id")] string entityId,
        [FromServices] IPortalConversationService service,
        CancellationToken ct)
    {
        var result = await service.GetConversationsAsync(userId, entityId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateConversation(
        [FromBody] CreateConversationRequest request,
        [FromHeader(Name = "X-Portal-User-Id")] string userId,
        [FromServices] IPortalConversationService service,
        CancellationToken ct)
    {
        var result = await service.CreateConversationAsync(request, userId, ct);
        return Results.Created($"/api/v1/portal/conversations/{result.Id}", result);
    }

    private static async Task<IResult> GetConversation(
        string conversationId,
        [FromHeader(Name = "X-Portal-User-Id")] string userId,
        [FromServices] IPortalConversationService service,
        CancellationToken ct)
    {
        var result = await service.GetConversationAsync(conversationId, userId, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> DeleteConversation(
        string conversationId,
        [FromHeader(Name = "X-Portal-User-Id")] string userId,
        [FromServices] IPortalConversationService service,
        CancellationToken ct)
    {
        await service.DeleteConversationAsync(conversationId, userId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> SendMessage(
        string conversationId,
        [FromBody] SendMessageRequest request,
        [FromHeader(Name = "X-Portal-User-Id")] string userId,
        [FromServices] IPortalConversationService service,
        CancellationToken ct)
    {
        var result = await service.SendMessageAsync(conversationId, userId, request, ct);
        return Results.Ok(result);
    }
}
