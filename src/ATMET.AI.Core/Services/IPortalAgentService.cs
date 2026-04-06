using ATMET.AI.Core.Models.Portal;

namespace ATMET.AI.Core.Services;

/// <summary>
/// AI agent service for the Client Portal chat experience.
/// Processes user messages, calls tools (which map to IPortal*Service interfaces),
/// and streams structured responses via SSE.
/// </summary>
public interface IPortalAgentService
{
    /// <summary>
    /// Process a user message in a portal conversation.
    /// Yields SSE events: typing → tool_call(s) → message(s) → done.
    /// </summary>
    /// <param name="conversationId">The conversation to process the message in.</param>
    /// <param name="userId">Portal user ID (from X-Portal-User-Id header).</param>
    /// <param name="entityId">Entity ID (from X-Portal-Entity-Id header).</param>
    /// <param name="userMessage">The user's chat message (may be text or a structured action).</param>
    /// <param name="language">User's preferred language ("en" or "ar").</param>
    /// <param name="cancellationToken">Cancellation token (client disconnect).</param>
    /// <returns>Async stream of <see cref="PortalChatEvent"/> for SSE serialization.</returns>
    IAsyncEnumerable<PortalChatEvent> ProcessMessageAsync(
        string conversationId,
        string userId,
        string entityId,
        PortalChatMessage userMessage,
        string language = "en",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the Azure persistent agent id for the portal assistant, creating it if missing.
    /// Name and instructions come from AzureAI configuration (<c>PortalAgentName</c>, <c>PortalAgentInstructions</c>).
    /// </summary>
    Task<string> GetOrCreatePortalAgentIdAsync(CancellationToken cancellationToken = default);
}
