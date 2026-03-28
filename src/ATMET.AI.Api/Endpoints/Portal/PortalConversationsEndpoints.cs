using ATMET.AI.Core.Models.Portal;
using ATMET.AI.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ATMET.AI.Api.Endpoints.Portal;

public static class PortalConversationsEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        var conversations = group.MapGroup("/portal/conversations")
            .WithTags("Portal - Conversations");

        conversations.MapGet("/", GetConversations)
            .WithName("GetPortalConversations")
            .WithSummary("List conversations for the authenticated user")
            .RequireAuthorization("ApiReader");

        conversations.MapPost("/", CreateConversation)
            .WithName("CreatePortalConversation")
            .WithSummary("Create a new conversation")
            .RequireAuthorization("ApiWriter");

        conversations.MapGet("/{conversationId}", GetConversation)
            .WithName("GetPortalConversation")
            .WithSummary("Get conversation with all messages")
            .RequireAuthorization("ApiReader");

        conversations.MapDelete("/{conversationId}", DeleteConversation)
            .WithName("DeletePortalConversation")
            .WithSummary("Delete a conversation")
            .RequireAuthorization("ApiWriter");

        conversations.MapPost("/{conversationId}/messages", SendMessage)
            .WithName("SendPortalMessage")
            .WithSummary("Send a message in a conversation")
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
