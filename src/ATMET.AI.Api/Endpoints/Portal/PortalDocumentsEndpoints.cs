using ATMET.AI.Core.Models.Portal;
using ATMET.AI.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ATMET.AI.Api.Endpoints.Portal;

/// <summary>
/// Case document uploads and checklist (Supabase + storage).
/// </summary>
public static class PortalDocumentsEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        var docs = group.MapGroup("/portal/cases/{caseId}/documents")
            .WithTags("Portal - Documents");

        // Literal segment must be registered before `{docId}` so "checklist" is not captured as an id.
        docs.MapGet("/checklist", GetDocumentChecklist)
            .WithName("GetPortalDocumentChecklist")
            .WithSummary("Get required documents checklist with upload status")
            .WithDescription("""
                Returns the **required document catalog** for the case’s service merged with **per-item upload status** (uploaded file id/name when satisfied).

                **Business use:** render the “upload required documents” step and agent **`document_request`** cards.

                **Headers:** `X-Portal-User-Id` (required).
                """)
            .Produces<List<DocumentChecklistItemResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization("ApiReader");

        docs.MapPost("/", UploadDocument)
            .WithName("UploadPortalDocument")
            .WithSummary("Upload a document for a case")
            .WithDescription("""
                **`multipart/form-data`** with a single file field. Optional query **`documentCatalogId`** ties the upload to a checklist row.

                **Response:** `201 Created` with **`PortalDocumentResponse`** (includes storage URL and validation status).

                **Headers:** `X-Portal-User-Id` (required).
                """)
            .RequireAuthorization("ApiWriter")
            .DisableAntiforgery()
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<PortalDocumentResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        docs.MapGet("/", GetDocuments)
            .WithName("GetPortalDocuments")
            .WithSummary("List documents for a case")
            .WithDescription("""
                Lists **all uploaded documents** for the case with metadata and validation flags.

                **Headers:** `X-Portal-User-Id` (required).
                """)
            .Produces<List<PortalDocumentResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization("ApiReader");

        docs.MapGet("/{docId}", GetDocument)
            .WithName("GetPortalDocument")
            .WithSummary("Get document detail with signed download URL")
            .WithDescription("""
                Returns **one document** including a **time-limited file URL** suitable for download or preview.

                **Headers:** `X-Portal-User-Id` (required). **`404`** if not found.
                """)
            .Produces<PortalDocumentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
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
