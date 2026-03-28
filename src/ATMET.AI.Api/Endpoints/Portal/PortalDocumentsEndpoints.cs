using ATMET.AI.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ATMET.AI.Api.Endpoints.Portal;

public static class PortalDocumentsEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        var docs = group.MapGroup("/portal/cases/{caseId}/documents")
            .WithTags("Portal - Documents");

        docs.MapPost("/", UploadDocument)
            .WithName("UploadPortalDocument")
            .WithSummary("Upload a document for a case")
            .RequireAuthorization("ApiWriter")
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data");

        docs.MapGet("/", GetDocuments)
            .WithName("GetPortalDocuments")
            .WithSummary("List documents for a case")
            .RequireAuthorization("ApiReader");

        docs.MapGet("/{docId}", GetDocument)
            .WithName("GetPortalDocument")
            .WithSummary("Get document detail with signed download URL")
            .RequireAuthorization("ApiReader");

        docs.MapGet("/checklist", GetDocumentChecklist)
            .WithName("GetPortalDocumentChecklist")
            .WithSummary("Get required documents checklist with upload status")
            .RequireAuthorization("ApiReader");
    }

    private static async Task<IResult> UploadDocument(
        string caseId,
        [FromHeader(Name = "X-Portal-User-Id")] string userId,
        IFormFile file,
        [FromQuery] string? documentCatalogId,
        [FromServices] IPortalDocumentService service,
        CancellationToken ct)
    {
        using var stream = file.OpenReadStream();
        var result = await service.UploadDocumentAsync(
            caseId, userId, stream, file.FileName,
            file.ContentType, file.Length, documentCatalogId, ct);
        return Results.Created($"/api/v1/portal/cases/{caseId}/documents/{result.Id}", result);
    }

    private static async Task<IResult> GetDocuments(
        string caseId,
        [FromHeader(Name = "X-Portal-User-Id")] string userId,
        [FromServices] IPortalDocumentService service,
        CancellationToken ct)
    {
        var result = await service.GetDocumentsAsync(caseId, userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetDocument(
        string caseId,
        string docId,
        [FromHeader(Name = "X-Portal-User-Id")] string userId,
        [FromServices] IPortalDocumentService service,
        CancellationToken ct)
    {
        var result = await service.GetDocumentAsync(caseId, docId, userId, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> GetDocumentChecklist(
        string caseId,
        [FromHeader(Name = "X-Portal-User-Id")] string userId,
        [FromServices] IPortalDocumentService service,
        CancellationToken ct)
    {
        var result = await service.GetDocumentChecklistAsync(caseId, userId, ct);
        return Results.Ok(result);
    }
}
