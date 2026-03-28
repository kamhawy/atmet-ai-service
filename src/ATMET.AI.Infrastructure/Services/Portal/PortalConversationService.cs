using ATMET.AI.Core.Models.Portal;
using ATMET.AI.Core.Services;
using ATMET.AI.Infrastructure.Clients;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ATMET.AI.Infrastructure.Services.Portal;

public class PortalConversationService : IPortalConversationService
{
    private readonly SupabaseRestClient _db;
    private readonly ILogger<PortalConversationService> _logger;

    public PortalConversationService(SupabaseRestClient db, ILogger<PortalConversationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<PortalConversationSummaryResponse>> GetConversationsAsync(string userId, string entityId, CancellationToken ct = default)
    {
        var rows = await _db.GetAsync<JsonElement>("conversations",
            select: "id,title,status,case_id,service_id,messages,created_at,updated_at",
            filters: [$"user_id=eq.{userId}", $"entity_id=eq.{entityId}"],
            order: "updated_at.desc",
            cancellationToken: ct);

        return rows.Select(r =>
        {
            var messages = ParseMessages(r);
            var lastMsg = messages.LastOrDefault();

            return new PortalConversationSummaryResponse(
                Id: r.GetProperty("id").GetString()!,
                Title: r.GetProp("title"),
                Status: r.GetProperty("status").GetString()!,
                CaseId: r.GetProp("case_id"),
                ServiceId: r.GetProp("service_id"),
                LastMessage: lastMsg?.Content,
                LastMessageAt: lastMsg?.Timestamp,
                MessageCount: messages.Count,
                CreatedAt: r.GetProperty("created_at").GetDateTimeOffset(),
                UpdatedAt: r.GetProperty("updated_at").GetDateTimeOffset()
            );
        }).ToList();
    }

    public async Task<PortalConversationResponse> CreateConversationAsync(CreateConversationRequest request, string userId, CancellationToken ct = default)
    {
        var data = new Dictionary<string, object?>
        {
            ["user_id"] = userId,
            ["entity_id"] = request.EntityId,
            ["case_id"] = request.CaseId,
            ["service_id"] = request.ServiceId,
            ["title"] = request.Title,
            ["messages"] = new object[] { },
            ["status"] = "active"
        };

        var row = await _db.InsertAsync<JsonElement>("conversations", data, ct);

        return new PortalConversationResponse(
            Id: row.GetProperty("id").GetString()!,
            Title: row.GetProp("title"),
            Status: row.GetProperty("status").GetString()!,
            CaseId: row.GetProp("case_id"),
            ServiceId: row.GetProp("service_id"),
            Messages: [],
            FormData: row.GetJsonProp("form_data"),
            CreatedAt: row.GetProperty("created_at").GetDateTimeOffset(),
            UpdatedAt: row.GetProperty("updated_at").GetDateTimeOffset()
        );
    }

    public async Task<PortalConversationResponse?> GetConversationAsync(string conversationId, string userId, CancellationToken ct = default)
    {
        var r = await _db.GetByIdAsync<JsonElement>("conversations", conversationId, cancellationToken: ct);
        if (r.ValueKind == JsonValueKind.Undefined) return null;

        // Verify ownership
        if (r.GetProperty("user_id").GetString() != userId)
            return null;

        return new PortalConversationResponse(
            Id: r.GetProperty("id").GetString()!,
            Title: r.GetProp("title"),
            Status: r.GetProperty("status").GetString()!,
            CaseId: r.GetProp("case_id"),
            ServiceId: r.GetProp("service_id"),
            Messages: ParseMessages(r),
            FormData: r.GetJsonProp("form_data"),
            CreatedAt: r.GetProperty("created_at").GetDateTimeOffset(),
            UpdatedAt: r.GetProperty("updated_at").GetDateTimeOffset()
        );
    }

    public async Task DeleteConversationAsync(string conversationId, string userId, CancellationToken ct = default)
    {
        // Verify ownership first
        var existing = await _db.GetByIdAsync<JsonElement>("conversations", conversationId, cancellationToken: ct);
        if (existing.ValueKind == JsonValueKind.Undefined) return;
        if (existing.GetProperty("user_id").GetString() != userId) return;

        await _db.DeleteAsync("conversations", conversationId, cancellationToken: ct);
    }

    public async Task<PortalMessageResponse> SendMessageAsync(string conversationId, string userId, SendMessageRequest request, CancellationToken ct = default)
    {
        // Get current conversation
        var conv = await _db.GetByIdAsync<JsonElement>("conversations", conversationId, cancellationToken: ct);
        if (conv.ValueKind == JsonValueKind.Undefined)
            throw new ATMET.AI.Core.Exceptions.NotFoundException($"Conversation {conversationId} not found");

        if (conv.GetProperty("user_id").GetString() != userId)
            throw new ATMET.AI.Core.Exceptions.NotFoundException($"Conversation {conversationId} not found");

        // Parse existing messages
        var messages = ParseMessagesRaw(conv);

        // Create new message
        var newMessage = new Dictionary<string, object?>
        {
            ["id"] = Guid.NewGuid().ToString(),
            ["role"] = "user",
            ["type"] = request.Type ?? "text",
            ["content"] = request.Content,
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("o")
        };

        messages.Add(JsonSerializer.SerializeToElement(newMessage));

        // Update conversation with appended message
        await _db.UpdateAsync<JsonElement>("conversations", conversationId,
            new { messages }, cancellationToken: ct);

        var msg = new PortalMessageResponse(
            Id: newMessage["id"]!.ToString()!,
            Role: "user",
            Content: request.Content,
            Type: request.Type ?? "text",
            Timestamp: DateTimeOffset.UtcNow
        );

        _logger.LogInformation("Message sent to conversation {ConversationId}", conversationId);

        return msg;
    }

    private static List<PortalMessageResponse> ParseMessages(JsonElement row)
    {
        if (!row.TryGetProperty("messages", out var messagesEl) ||
            messagesEl.ValueKind != JsonValueKind.Array)
            return [];

        var result = new List<PortalMessageResponse>();
        foreach (var m in messagesEl.EnumerateArray())
        {
            result.Add(new PortalMessageResponse(
                Id: m.GetProp("id") ?? Guid.NewGuid().ToString(),
                Role: m.GetProp("role") ?? "user",
                Content: m.GetProp("content") ?? "",
                Type: m.GetProp("type"),
                Timestamp: m.TryGetProperty("timestamp", out var ts)
                    ? DateTimeOffset.Parse(ts.GetString()!)
                    : DateTimeOffset.MinValue,
                Attachments: m.GetJsonProp("attachments"),
                DocumentScan: m.GetJsonProp("documentScan"),
                FormCard: m.GetJsonProp("formCard")
            ));
        }
        return result;
    }

    private static List<JsonElement> ParseMessagesRaw(JsonElement row)
    {
        if (!row.TryGetProperty("messages", out var messagesEl) ||
            messagesEl.ValueKind != JsonValueKind.Array)
            return [];

        return messagesEl.EnumerateArray().ToList();
    }
}
