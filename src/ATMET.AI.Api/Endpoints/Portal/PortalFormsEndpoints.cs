using ATMET.AI.Core.Models.Portal;
using ATMET.AI.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ATMET.AI.Api.Endpoints.Portal;

public static class PortalFormsEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        var forms = group.MapGroup("/portal/cases/{caseId}/form")
            .WithTags("Portal - Forms");

        forms.MapGet("/", GetForm)
            .WithName("GetPortalForm")
            .WithSummary("Get form schema and current data for a case")
            .RequireAuthorization("ApiReader");

        forms.MapPatch("/", UpdateFormData)
            .WithName("UpdatePortalFormData")
            .WithSummary("Update form field values (partial save)")
            .RequireAuthorization("ApiWriter");

        forms.MapPost("/validate", ValidateForm)
            .WithName("ValidatePortalForm")
            .WithSummary("Validate form data against rules")
            .RequireAuthorization("ApiReader");

        forms.MapPost("/submit", SubmitForm)
            .WithName("SubmitPortalForm")
            .WithSummary("Submit the case with finalized form data")
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
