using System.Text.Json;

namespace ATMET.AI.Core.Models.Portal;

/// <summary>
/// Partial update for Foundry / workflow session fields on <c>conversations</c> (Supabase).
/// Only non-null properties are sent to PostgREST, except <see cref="ClearPauseFields"/> which explicitly sets pause columns to SQL NULL.
/// </summary>
public record FoundryConversationSessionPatch(
    string? FoundryProjectConversationId = null,
    string? FoundryRunId = null,
    string? LastResponseId = null,
    string? PauseUiAction = null,
    string? PauseWaitingFor = null,
    JsonElement? PauseEnvelope = null,
    string? FoundryCurrentStep = null,
    string? ConversationLanguage = null,
    bool ClearPauseFields = false);
