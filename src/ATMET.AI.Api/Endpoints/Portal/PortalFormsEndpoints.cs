using ATMET.AI.Core.Models.Portal;
using ATMET.AI.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ATMET.AI.Api.Endpoints.Portal;

/// <summary>
/// Dynamic form schema and data for a portal case.
/// </summary>
public static class PortalFormsEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        var forms = group.MapGroup("/portal/cases/{caseId}/form")
            .WithTags("Portal - Forms");

        forms.MapGet("/", GetForm)
            .WithName("GetPortalForm")
            .WithSummary("Get form schema and current data for a case")
            .WithDescription("""
                Returns **`formSchema`** (JSON) and **`currentData`** (saved field values) for the case’s service.

                **Business use:** render React Hook Form / JSON-schema driven UIs and agent **`form_request`** payloads.

                **Headers:** `X-Portal-User-Id` (required). **`404`** if the case is not accessible.
                """)
            .Produces<PortalFormResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization("ApiReader");

        forms.MapPatch("/", UpdateFormData)
            .WithName("UpdatePortalFormData")
            .WithSummary("Update form field values (partial save)")
            .WithDescription("""
                **`PATCH`** body: `{ "formData": { ... } }` — merges into persisted case JSON (**autosave**).

                **Business use:** incremental wizard saves without submitting the case.

                **Headers:** `X-Portal-User-Id` (required).
                """)
            .Produces<PortalFormResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization("ApiWriter");

        forms.MapPost("/validate", ValidateForm)
            .WithName("ValidatePortalForm")
            .WithSummary("Validate form data against rules")
            .WithDescription("""
                Evaluates **`formData`** against service validation / ruleset logic **without** persisting a final submission.

                **Response:** `FormValidationResponse` with **`isValid`**, **`errors`** array, and optional **`eligibilityResult`**.

                **Headers:** `X-Portal-User-Id` (required).
                """)
            .Produces<FormValidationResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization("ApiReader");

        forms.MapPost("/submit", SubmitForm)
            .WithName("SubmitPortalForm")
            .WithSummary("Submit the case with finalized form data")
            .WithDescription("""
                Finalizes the application: optional **`formData`** (if omitted, stored data is used), optional **`comment`**.

                **Business use:** citizen clicks “Submit”; may transition status and workflow per backend rules.

                **Headers:** `X-Portal-User-Id` (required).
                """)
            .Produces<PortalCaseResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization("ApiWriter");
    }

    private static async Task<IResult> GetForm(
        string caseId,
        [FromHeader(Name = "X-Portal-User-Id")] string userId,
        [FromServices] IPortalFormService service,
        CancellationToken ct)
    {
        var result = await service.GetFormAsync(caseId, userId, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> UpdateFormData(
        string caseId,
        [FromBody] UpdateFormDataRequest request,
        [FromHeader(Name = "X-Portal-User-Id")] string userId,
        [FromServices] IPortalFormService service,
        CancellationToken ct)
    {
        var result = await service.UpdateFormDataAsync(caseId, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ValidateForm(
        string caseId,
        [FromBody] ValidateFormRequest request,
        [FromHeader(Name = "X-Portal-User-Id")] string userId,
        [FromServices] IPortalFormService service,
        CancellationToken ct)
    {
        var result = await service.ValidateFormAsync(caseId, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> SubmitForm(
        string caseId,
        [FromBody] SubmitFormRequest request,
        [FromHeader(Name = "X-Portal-User-Id")] string userId,
        [FromServices] IPortalFormService service,
        CancellationToken ct)
    {
        var result = await service.SubmitFormAsync(caseId, userId, request, ct);
        return Results.Ok(result);
    }
}
