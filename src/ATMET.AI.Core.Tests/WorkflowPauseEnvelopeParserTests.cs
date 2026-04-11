using System.Text.Json;
using ATMET.AI.Core.Models.PortalAiWorkflow;
using Xunit;

namespace ATMET.AI.Core.Tests;

public class WorkflowPauseEnvelopeParserTests
{
    [Fact]
    public void TryMerge_PdfStyleSnakeCase_SetsRefsAndRaw()
    {
        var json = """
            {"status":"paused","ui_action":"render_form","waiting_for":"form_submission","run_id":"run_1",
             "message_to_user":{"en":"Fill the form","ar":"املأ النموذج"},
             "required_fields":[{"id":"tin"}]}
            """;

        string? wire = null;
        string? ui = null;
        string? wf = null;
        string? rid = null;
        JsonElement? raw = null;

        var ok = WorkflowPauseEnvelopeParser.TryMergeFromJsonObjectString(
            json.Trim(), ref wire, ref ui, ref wf, ref rid, ref raw);

        Assert.True(ok);
        Assert.Equal(PortalAiWorkflowStatuses.PausedForHitl, wire);
        Assert.Equal("render_form", ui);
        Assert.Equal("form_submission", wf);
        Assert.Equal("run_1", rid);
        Assert.NotNull(raw);
        Assert.True(raw.Value.TryGetProperty("messageToUser", out var mtu));
        Assert.True(mtu.TryGetProperty("en", out _));
    }

    [Fact]
    public void TryMerge_LegacyCamelCase_StillWorks()
    {
        var json = """{"pause":true,"uiAction":"choose_service","waitingFor":"Pick one"}""";

        string? wire = null;
        string? ui = null;
        string? wf = null;
        string? rid = null;
        JsonElement? raw = null;

        var ok = WorkflowPauseEnvelopeParser.TryMergeFromJsonObjectString(
            json, ref wire, ref ui, ref wf, ref rid, ref raw);

        Assert.True(ok);
        Assert.Equal(PortalAiWorkflowStatuses.PausedForHitl, wire);
        Assert.Equal("choose_service", ui);
        Assert.Equal("Pick one", wf);
    }

    [Fact]
    public void TryMerge_NonObject_ReturnsFalse()
    {
        string? wire = null;
        string? ui = null;
        string? wf = null;
        string? rid = null;
        JsonElement? raw = null;

        Assert.False(WorkflowPauseEnvelopeParser.TryMergeFromJsonObjectString(
            "hello", ref wire, ref ui, ref wf, ref rid, ref raw));
    }

    [Fact]
    public void WorkflowPauseUiActions_KnownSet()
    {
        Assert.True(WorkflowPauseUiActions.IsKnown(WorkflowPauseUiActions.RenderForm));
        Assert.False(WorkflowPauseUiActions.IsKnown("unknown_action"));
    }

    [Fact]
    public void TryMerge_PdfAppendixFields_NormalizedToCamelCase()
    {
        var json = """
            {"status":"paused","ui_action":"upload_documents","waiting_for":"document_upload",
             "required_documents":[{"binding_id":"b1","document_id":"d1","name_en":"Qatar ID"}],
             "extracted_fields":[{"field_id":"tin","value":"123"}],
             "advisory_result":{"is_authoritative":false},
             "next_action":{"resume_endpoint":"/x"},
             "debug":{"route":"B"}}
            """;

        string? wire = null;
        string? ui = null;
        string? wf = null;
        string? rid = null;
        JsonElement? raw = null;

        var ok = WorkflowPauseEnvelopeParser.TryMergeFromJsonObjectString(
            json.Trim(), ref wire, ref ui, ref wf, ref rid, ref raw);

        Assert.True(ok);
        Assert.NotNull(raw);
        Assert.True(raw.Value.TryGetProperty("requiredDocuments", out var rd) && rd.GetArrayLength() == 1);
        Assert.True(raw.Value.TryGetProperty("extractedFields", out var ef) && ef.GetArrayLength() == 1);
        Assert.True(raw.Value.TryGetProperty("advisoryResult", out _));
        Assert.True(raw.Value.TryGetProperty("nextAction", out _));
        Assert.True(raw.Value.TryGetProperty("debug", out _));
    }
}
