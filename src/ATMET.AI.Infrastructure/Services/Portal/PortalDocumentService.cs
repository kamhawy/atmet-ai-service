using ATMET.AI.Core.Models.Portal;
using ATMET.AI.Core.Services;
using ATMET.AI.Infrastructure.Clients;
using ATMET.AI.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ATMET.AI.Infrastructure.Services.Portal;

public class PortalDocumentService : IPortalDocumentService
{
    private readonly SupabaseRestClient _db;
    private readonly SupabaseOptions _options;
    private readonly ILogger<PortalDocumentService> _logger;

    public PortalDocumentService(
        SupabaseRestClient db,
        IOptions<SupabaseOptions> options,
        ILogger<PortalDocumentService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PortalDocumentResponse> UploadDocumentAsync(
        string caseId, string userId, Stream fileStream, string fileName,
        string contentType, long fileSize, string? documentCatalogId, CancellationToken ct = default)
    {
        await PortalAuthorizationHelper.VerifyCaseOwnershipAsync(_db, caseId, userId, ct);

        // Upload file to Supabase Storage
        var storagePath = $"{caseId}/{Guid.NewGuid()}/{fileName}";
        await _db.UploadFileAsync(_options.DocumentsBucket, storagePath, fileStream, contentType, ct);

        // Get a signed URL for the uploaded file
        var fileUrl = await _db.GetSignedUrlAsync(_options.DocumentsBucket, storagePath, 86400, ct);

        // Insert record into case_documents
        var data = new Dictionary<string, object?>
        {
            ["case_id"] = caseId,
            ["document_id"] = documentCatalogId,
            ["file_url"] = storagePath, // Store the path, generate signed URL on read
            ["file_name"] = fileName,
            ["file_size"] = fileSize,
            ["mime_type"] = contentType,
            ["validation_status"] = "pending"
        };

        var row = await _db.InsertAsync<JsonElement>("case_documents", data, ct);

        _logger.LogInformation("Document uploaded for case {CaseId}: {FileName}", caseId, fileName);

        return MapDocumentResponse(row, fileUrl);
    }

    public async Task<List<PortalDocumentResponse>> GetDocumentsAsync(string caseId, string userId, CancellationToken ct = default)
    {
        await PortalAuthorizationHelper.VerifyCaseOwnershipAsync(_db, caseId, userId, ct);

        var rows = await _db.GetAsync<JsonElement>("case_documents",
            filters: [$"case_id=eq.{caseId}"],
            order: "created_at.desc",
            cancellationToken: ct);

        var results = new List<PortalDocumentResponse>();
        foreach (var r in rows)
        {
            var path = r.GetProperty("file_url").GetString()!;
            string signedUrl;
            try
            {
                signedUrl = await _db.GetSignedUrlAsync(_options.DocumentsBucket, path, 3600, ct);
            }
            catch
            {
                signedUrl = path; // Fallback to stored path
            }
            results.Add(MapDocumentResponse(r, signedUrl));
        }
        return results;
    }

    public async Task<PortalDocumentResponse?> GetDocumentAsync(string caseId, string docId, string userId, CancellationToken ct = default)
    {
        await PortalAuthorizationHelper.VerifyCaseOwnershipAsync(_db, caseId, userId, ct);

        var row = await _db.GetByIdAsync<JsonElement>("case_documents", docId, cancellationToken: ct);
        if (row.ValueKind == JsonValueKind.Undefined) return null;
        if (row.GetProperty("case_id").GetString() != caseId) return null;

        var path = row.GetProperty("file_url").GetString()!;
        string signedUrl;
        try
        {
            signedUrl = await _db.GetSignedUrlAsync(_options.DocumentsBucket, path, 3600, ct);
        }
        catch
        {
            signedUrl = path;
        }
        return MapDocumentResponse(row, signedUrl);
    }

    public async Task<List<DocumentChecklistItemResponse>> GetDocumentChecklistAsync(string caseId, string userId, CancellationToken ct = default)
    {
        await PortalAuthorizationHelper.VerifyCaseOwnershipAsync(_db, caseId, userId, ct);

        // Get the case to find service_id
        var caseRow = await _db.GetByIdAsync<JsonElement>("cases", caseId,
            select: "service_id", cancellationToken: ct);
        if (caseRow.ValueKind == JsonValueKind.Undefined) return [];

        var serviceId = caseRow.GetProperty("service_id").GetString()!;

        // Get required documents for this service
        var serviceDocuments = await _db.GetAsync<JsonElement>("service_documents",
            select: "id,document_id,is_required",
            filters: [$"service_id=eq.{serviceId}"],
            cancellationToken: ct);

        if (serviceDocuments.Count == 0) return [];

        // Get document catalog details
        var catalogIds = serviceDocuments
            .Select(sd => sd.GetProperty("document_id").GetString()!)
            .Distinct().ToList();

        var catalogMap = new Dictionary<string, JsonElement>();
        foreach (var catId in catalogIds)
        {
            var cat = await _db.GetByIdAsync<JsonElement>("documents_catalog", catId, cancellationToken: ct);
            if (cat.ValueKind != JsonValueKind.Undefined) catalogMap[catId] = cat;
        }

        // Get uploaded documents for this case
        var caseDocuments = await _db.GetAsync<JsonElement>("case_documents",
            select: "id,document_id,file_name,validation_status",
            filters: [$"case_id=eq.{caseId}"],
            cancellationToken: ct);

        var uploadedByDocId = caseDocuments
            .Where(cd => cd.GetProp("document_id") != null)
            .GroupBy(cd => cd.GetProperty("document_id").GetString()!)
            .ToDictionary(g => g.Key, g => g.First());

        // Build checklist
        return serviceDocuments.Select(sd =>
        {
            var docCatalogId = sd.GetProperty("document_id").GetString()!;
            var isRequired = sd.TryGetProperty("is_required", out var req) && req.GetBoolean();

            catalogMap.TryGetValue(docCatalogId, out var catalog);
            uploadedByDocId.TryGetValue(docCatalogId, out var uploaded);

            string uploadStatus = uploaded.ValueKind != JsonValueKind.Undefined ? "uploaded" : (isRequired ? "required" : "not_required_yet");

            return new DocumentChecklistItemResponse(
                DocumentCatalogId: docCatalogId,
                NameEn: catalog.ValueKind != JsonValueKind.Undefined ? catalog.GetProp("name_en") ?? "" : "",
                NameAr: catalog.ValueKind != JsonValueKind.Undefined ? catalog.GetProp("name_ar") ?? "" : "",
                IsRequired: isRequired,
                UploadStatus: uploadStatus,
                UploadedDocumentId: uploaded.ValueKind != JsonValueKind.Undefined ? uploaded.GetProp("id") : null,
                UploadedFileName: uploaded.ValueKind != JsonValueKind.Undefined ? uploaded.GetProp("file_name") : null
            );
        }).ToList();
    }

    private static PortalDocumentResponse MapDocumentResponse(JsonElement r, string signedUrl)
    {
        return new PortalDocumentResponse(
            Id: r.GetProperty("id").GetString()!,
            CaseId: r.GetProperty("case_id").GetString()!,
            DocumentCatalogId: r.GetProp("document_id"),
            FileName: r.GetProperty("file_name").GetString()!,
            FileUrl: signedUrl,
            FileSize: r.GetIntProp("file_size"),
            MimeType: r.GetProp("mime_type"),
            ValidationStatus: r.GetProperty("validation_status").GetString()!,
            Notes: r.GetProp("notes"),
            CreatedAt: r.GetProperty("created_at").GetDateTimeOffset()
        );
    }
}
