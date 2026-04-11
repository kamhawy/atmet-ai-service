using ATMET.AI.Core.Models.Portal;
using ATMET.AI.Core.Services;
using ATMET.AI.Infrastructure.Clients;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ATMET.AI.Infrastructure.Services.Portal;

public class PortalConversationService : IPortalConversationService
{
    private const string ConversationSelectColumns =
        "id,user_id,title,status,case_id,service_id,messages,form_data,created_at,updated_at," +
        "foundry_project_conversation_id,foundry_run_id,last_response_id,pause_ui_action,pause_waiting_for,pause_envelope," +
        "foundry_current_step,conversation_language";

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

        return MapConversationRow(row, messages: []);
    }

    public async Task<PortalConversationResponse?> GetConversationAsync(string conversationId, string userId, CancellationToken ct = default)
    {
        var r = await _db.GetByIdAsync<JsonElement>(
            "conversations",
            conversationId,
            select: ConversationSelectColumns,
            cancellationToken: ct);
        if (r.ValueKind == JsonValueKind.Undefined) return null;

        // Verify ownership
        if (r.GetProperty("user_id").GetString() != userId)
            return null;

        return MapConversationRow(r, messages: ParseMessages(r));
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

    public async Task<PortalConversationResponse?> UpdateFoundrySessionAsync(
        string conversationId,
        string userId,
        FoundryConversationSessionPatch patch,
        CancellationToken ct = default)
    {
        var conv = await _db.GetByIdAsync<JsonElement>("conversations", conversationId, cancellationToken: ct);
        if (conv.ValueKind == JsonValueKind.Undefined) return null;
        if (conv.GetProperty("user_id").GetString() != userId) return null;

        var body = new Dictionary<string, object?>();
        if (patch.FoundryProjectConversationId is not null)
            body["foundry_project_conversation_id"] = patch.FoundryProjectConversationId;
        if (patch.FoundryRunId is not null)
            body["foundry_run_id"] = patch.FoundryRunId;
        if (patch.LastResponseId is not null)
            body["last_response_id"] = patch.LastResponseId;
        if (patch.PauseUiAction is not null)
            body["pause_ui_action"] = patch.PauseUiAction;
        if (patch.PauseWaitingFor is not null)
            body["pause_waiting_for"] = patch.PauseWaitingFor;
        if (patch.PauseEnvelope is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null })
            body["pause_envelope"] = patch.PauseEnvelope;
        if (patch.FoundryCurrentStep is not null)
            body["foundry_current_step"] = patch.FoundryCurrentStep;
        if (patch.ConversationLanguage is not null)
            body["conversation_language"] = patch.ConversationLanguage;

        // Applied after explicit pause fields so a completed turn can clear stale pause_* columns.
        if (patch.ClearPauseFields)
        {
            body["pause_ui_action"] = null;
            body["pause_waiting_for"] = null;
            body["pause_envelope"] = null;
        }

        if (body.Count == 0)
            return await GetConversationAsync(conversationId, userId, ct);

        var serializerOptions = body.Values.Any(static v => v is null)
            ? SupabaseRestClient.JsonOptionsIncludeNulls
            : null;

        var updated = await _db.UpdateAsync<JsonElement>(
            "conversations",
            conversationId,
            body,
            cancellationToken: ct,
            serializerOptions: serializerOptions);

        return MapConversationRow(updated, messages: ParseMessages(updated));
    }

    private static PortalConversationResponse MapConversationRow(JsonElement r, List<PortalMessageResponse> messages) =>
        new(
            Id: r.GetProperty("id").GetString()!,
            Title: r.GetProp("title"),
            Status: r.GetProperty("status").GetString()!,
            CaseId: r.GetProp("case_id"),
            ServiceId: r.GetProp("service_id"),
            Messages: messages,
            FormData: r.GetJsonProp("form_data"),
            CreatedAt: r.GetProperty("created_at").GetDateTimeOffset(),
            UpdatedAt: r.GetProperty("updated_at").GetDateTimeOffset(),
            FoundryProjectConversationId: r.GetProp("foundry_project_conversation_id"),
            FoundryRunId: r.GetProp("foundry_run_id"),
            LastResponseId: r.GetProp("last_response_id"),
            PauseUiAction: r.GetProp("pause_ui_action"),
            PauseWaitingFor: r.GetProp("pause_waiting_for"),
            PauseEnvelope: r.GetJsonProp("pause_envelope"),
            FoundryCurrentStep: r.GetProp("foundry_current_step"),
            ConversationLanguage: r.GetProp("conversation_language")
        );

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
                FormCard: m.GetJsonProp("formCard"),
                Data: m.GetJsonProp("data")
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
