using System.Collections;
using System.Reflection;
using System.Text.Json;
using ATMET.AI.Core.Models.PortalAiWorkflow;
using OpenAI.Responses;

namespace ATMET.AI.Infrastructure.Services.PortalAiWorkflow;

/// <summary>
/// Maps <see cref="ResponseResult"/> + assistant output into workflow status and optional HITL pause hints.
/// Uses reflection for SDK <c>Status</c> (shape varies by package revision), <see cref="WorkflowPauseEnvelopeParser"/>
/// for PDF-aligned pause JSON, and <see cref="ResponseResult.OutputItems"/> for tool output strings not included in <c>GetOutputText()</c>.
/// </summary>
internal static class PortalAiWorkflowResponseMapper
{
    private static readonly string[] ToolLikeJsonPropertyNames =
        ["Output", "Content", "Result", "Text", "Arguments"];

    public static (string Status, string? UiAction, string? WaitingFor, string? RunId, JsonElement? RawPause) Map(
        ResponseResult response,
        string? outputText,
        JsonElement? resumePayload)
    {
        var wireStatus = TryGetPropertyString(response, "Status");
        string? ui = null;
        string? waitingFor = null;
        string? runId = null;
        JsonElement? rawPause = null;

        if (!string.IsNullOrWhiteSpace(outputText))
        {
            var t = outputText.Trim();
            WorkflowPauseEnvelopeParser.TryMergeFromJsonObjectString(
                t, ref wireStatus, ref ui, ref waitingFor, ref runId, ref rawPause);
        }

        TryMergePauseFromToolOutputItems(response, ref wireStatus, ref ui, ref waitingFor, ref runId, ref rawPause);

        if (resumePayload is { ValueKind: not JsonValueKind.Undefined and not JsonValueKind.Null })
            rawPause ??= resumePayload;

        var normalized = NormalizeStatus(wireStatus, ui, waitingFor);
        return (normalized, ui, waitingFor, runId, rawPause);
    }

    private static void TryMergePauseFromToolOutputItems(
        ResponseResult response,
        ref string? wireStatus,
        ref string? ui,
        ref string? waitingFor,
        ref string? runId,
        ref JsonElement? rawPause)
    {
        var prop = typeof(ResponseResult).GetProperty("OutputItems", BindingFlags.Public | BindingFlags.Instance);
        if (prop?.GetValue(response) is not IEnumerable items)
            return;

        foreach (var item in items)
        {
            if (item == null) continue;

            var typeName = item.GetType().Name;
            if (typeName.Contains("Message", StringComparison.OrdinalIgnoreCase) &&
                !typeName.Contains("Tool", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var fragment in ExtractJsonObjectStringsFromToolLikeItem(item))
            {
                WorkflowPauseEnvelopeParser.TryMergeFromJsonObjectString(
                    fragment.Trim(), ref wireStatus, ref ui, ref waitingFor, ref runId, ref rawPause);
            }
        }
    }

    private static IEnumerable<string> ExtractJsonObjectStringsFromToolLikeItem(object item)
    {
        var t = item.GetType();
        foreach (var name in ToolLikeJsonPropertyNames)
        {
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (p?.GetValue(item) is not string s) continue;
            var v = s.Trim();
            if (v.StartsWith('{') && v.EndsWith('}'))
                yield return v;
        }
    }

    private static string NormalizeStatus(string? wireStatus, string? uiAction, string? waitingFor)
    {
        if (!string.IsNullOrEmpty(uiAction) || !string.IsNullOrEmpty(waitingFor))
            return PortalAiWorkflowStatuses.PausedForHitl;

        if (string.IsNullOrWhiteSpace(wireStatus))
            return PortalAiWorkflowStatuses.Completed;

        var s = wireStatus.Trim();
        if (s.Equals("completed", StringComparison.OrdinalIgnoreCase))
            return PortalAiWorkflowStatuses.Completed;
        if (s.Equals("failed", StringComparison.OrdinalIgnoreCase))
            return PortalAiWorkflowStatuses.Failed;
        if (s.Equals("incomplete", StringComparison.OrdinalIgnoreCase))
            return PortalAiWorkflowStatuses.PausedForHitl;
        if (s.Contains("incomplete", StringComparison.OrdinalIgnoreCase))
            return PortalAiWorkflowStatuses.PausedForHitl;
        if (s.Equals("paused", StringComparison.OrdinalIgnoreCase))
            return PortalAiWorkflowStatuses.PausedForHitl;
        if (s.Equals(PortalAiWorkflowStatuses.PausedForHitl, StringComparison.OrdinalIgnoreCase))
            return PortalAiWorkflowStatuses.PausedForHitl;

        return s.ToLowerInvariant().Replace(' ', '_');
    }

    private static string? TryGetPropertyString(object target, string propertyName)
    {
        var p = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (p == null) return null;
        var v = p.GetValue(target);
        return v?.ToString();
    }
}
