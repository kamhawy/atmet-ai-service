using System.Text.Json;
using System.Text.Json.Nodes;

namespace ATMET.AI.Core.Models.PortalAiWorkflow;

/// <summary>
/// Parses rich HITL pause envelopes (portal workflow design) and legacy minimal control objects
/// (<c>pause</c> + <c>uiAction</c> + <c>waitingFor</c>) from assistant text or tool JSON fragments.
/// No JSON Schema validation — normalization and length limits only.
/// </summary>
public static class WorkflowPauseEnvelopeParser
{
    /// <summary>Maximum length for short string fields persisted to Supabase.</summary>
    public const int MaxPauseFieldLength = 2048;

    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= MaxPauseFieldLength ? value : value[..MaxPauseFieldLength];
    }

    /// <summary>
    /// If <paramref name="trimmedJson"/> is a JSON object with pause / workflow control fields,
    /// merges into ref parameters and sets <paramref name="rawPause"/> to a normalized envelope object.
    /// </summary>
    public static bool TryMergeFromJsonObjectString(
        string trimmedJson,
        ref string? wireStatus,
        ref string? uiAction,
        ref string? waitingFor,
        ref string? runId,
        ref JsonElement? rawPause)
    {
        if (string.IsNullOrWhiteSpace(trimmedJson) || !trimmedJson.StartsWith('{') || !trimmedJson.EndsWith('}'))
            return false;

        try
        {
            var node = JsonNode.Parse(trimmedJson);
            if (node is not JsonObject obj)
                return false;

            if (!LooksLikePauseOrWorkflowControl(obj))
                return false;

            if (TryGetString(obj, "status", out var st) &&
                !string.IsNullOrEmpty(st) &&
                st.Equals("paused", StringComparison.OrdinalIgnoreCase))
                wireStatus = PortalAiWorkflowStatuses.PausedForHitl;

            string? ua = null;
            _ = TryGetString(obj, "ui_action", out ua) || TryGetString(obj, "uiAction", out ua);
            if (!string.IsNullOrWhiteSpace(ua))
                uiAction = Sanitize(ua);

            string? wf = null;
            _ = TryGetString(obj, "waiting_for", out wf) || TryGetString(obj, "waitingFor", out wf);
            if (!string.IsNullOrWhiteSpace(wf))
                waitingFor = Sanitize(wf);

            string? r = null;
            _ = TryGetString(obj, "run_id", out r) || TryGetString(obj, "runId", out r);
            if (!string.IsNullOrWhiteSpace(r))
                runId = Sanitize(r);

            if (obj.TryGetPropertyValue("pause", out var pNode) && pNode is JsonValue pv)
            {
                try
                {
                    if (pv.TryGetValue(out bool pb) && pb)
                        wireStatus ??= PortalAiWorkflowStatuses.PausedForHitl;
                }
                catch (InvalidOperationException)
                {
                    /* not a bool */
                }
            }

            var normalized = NormalizeEnvelope(obj);
            rawPause = JsonSerializer.SerializeToElement(normalized);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool LooksLikePauseOrWorkflowControl(JsonObject obj) =>
        obj.ContainsKey("pause")
        || obj.ContainsKey("uiAction")
        || obj.ContainsKey("ui_action")
        || obj.ContainsKey("waitingFor")
        || obj.ContainsKey("waiting_for")
        || obj.ContainsKey("message_to_user")
        || obj.ContainsKey("messageToUser")
        || obj.ContainsKey("status")
        || obj.ContainsKey("required_fields")
        || obj.ContainsKey("requiredFields")
        || obj.ContainsKey("required_documents")
        || obj.ContainsKey("requiredDocuments")
        || obj.ContainsKey("extracted_fields")
        || obj.ContainsKey("extractedFields")
        || obj.ContainsKey("advisory_result")
        || obj.ContainsKey("advisoryResult")
        || obj.ContainsKey("next_action")
        || obj.ContainsKey("nextAction")
        || obj.ContainsKey("debug")
        || obj.ContainsKey("candidates");

    private static JsonObject NormalizeEnvelope(JsonObject src)
    {
        var o = new JsonObject();

        void copy(string snake, string camel, string outName)
        {
            if (src.TryGetPropertyValue(snake, out var a) && a != null)
            {
                o[outName] = a.DeepClone();
                return;
            }

            if (src.TryGetPropertyValue(camel, out var b) && b != null)
                o[outName] = b.DeepClone();
        }

        copy("run_id", "runId", "runId");
        copy("thread_id", "threadId", "threadId");
        copy("previous_response_id", "previousResponseId", "previousResponseId");
        copy("status", "status", "status");
        copy("language", "language", "language");
        copy("current_step", "currentStep", "currentStep");
        copy("agent_name", "agentName", "agentName");
        copy("message_to_user", "messageToUser", "messageToUser");
        copy("ui_action", "uiAction", "uiAction");
        copy("waiting_for", "waitingFor", "waitingFor");
        copy("service_id", "serviceId", "serviceId");
        copy("case_id", "caseId", "caseId");
        copy("required_fields", "requiredFields", "requiredFields");
        copy("service_name", "serviceName", "serviceName");
        copy("required_documents", "requiredDocuments", "requiredDocuments");
        copy("extracted_fields", "extractedFields", "extractedFields");
        copy("advisory_result", "advisoryResult", "advisoryResult");
        copy("next_action", "nextAction", "nextAction");
        copy("debug", "debug", "debug");
        copy("candidates", "candidates", "candidates");
        copy("missing_fields", "missingFields", "missingFields");
        copy("validation_issues", "validationIssues", "validationIssues");
        copy("overall_confidence", "overallConfidence", "overallConfidence");

        if (src.TryGetPropertyValue("pause", out var p) && p != null)
            o["pause"] = p.DeepClone();

        return o;
    }

    private static bool TryGetString(JsonObject obj, string name, out string? value)
    {
        value = null;
        if (!obj.TryGetPropertyValue(name, out var n) || n is null)
            return false;
        try
        {
            value = n.GetValue<string>();
            return !string.IsNullOrWhiteSpace(value);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
