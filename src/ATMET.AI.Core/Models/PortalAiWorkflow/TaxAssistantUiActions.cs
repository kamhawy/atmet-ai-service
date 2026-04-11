namespace ATMET.AI.Core.Models.PortalAiWorkflow;

/// <summary>
/// Canonical <c>ui_action</c> values from the Tax Assistant workflow design (PDF §5–§7).
/// The agent may emit additional strings; <see cref="IsKnown"/> is advisory for UI routing.
/// </summary>
public static class TaxAssistantUiActions
{
    public const string RenderMessage = "render_message";
    public const string AskClarification = "ask_clarification";
    public const string ChooseService = "choose_service";
    public const string RenderForm = "render_form";
    public const string UploadDocuments = "upload_documents";
    public const string ConfirmFields = "confirm_fields";
    public const string AwaitOfficerReview = "await_officer_review";
    public const string ShowDecision = "show_decision";

    private static readonly HashSet<string> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        RenderMessage,
        AskClarification,
        ChooseService,
        RenderForm,
        UploadDocuments,
        ConfirmFields,
        AwaitOfficerReview,
        ShowDecision
    };

    public static bool IsKnown(string? uiAction) =>
        !string.IsNullOrWhiteSpace(uiAction) && Known.Contains(uiAction.Trim());
}
