using ATMET.AI.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ATMET.AI.Infrastructure.Clients;

/// <summary>
/// Typed HTTP client for Supabase PostgREST and Storage APIs.
/// Registered as a singleton via HttpClientFactory.
/// </summary>
public class SupabaseRestClient
{
    private readonly HttpClient _http;
    private readonly SupabaseOptions _options;
    private readonly ILogger<SupabaseRestClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Use for PATCH bodies that must send JSON <c>null</c> (e.g. clearing nullable columns). Default <see cref="JsonOptions"/> omits nulls.
    /// </summary>
    public static readonly JsonSerializerOptions JsonOptionsIncludeNulls = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNameCaseInsensitive = true
    };

    public SupabaseRestClient(
        HttpClient http,
        IOptions<SupabaseOptions> options,
        ILogger<SupabaseRestClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // BaseAddress, apikey, and Authorization headers are configured
        // in the ConfigureHttpClient callback (ServiceCollectionExtensions)
        // so they are set BEFORE the resilience handler pipeline processes requests.
    }

    // ==================================================================================
    // PostgREST Query Methods
    // ==================================================================================

    /// <summary>
    /// GET rows from a table with optional PostgREST query parameters.
    /// </summary>
    /// <param name="table">Table name</param>
    /// <param name="select">PostgREST select clause (e.g. "id,name,services(*)")</param>
    /// <param name="filters">PostgREST filter params (e.g. "entity_id=eq.gta", "is_active=eq.true")</param>
    /// <param name="order">PostgREST order clause (e.g. "created_at.desc")</param>
    /// <param name="limit">Max rows</param>
    /// <param name="offset">Row offset for pagination</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<List<T>> GetAsync<T>(
        string table,
        string? select = null,
        IEnumerable<string>? filters = null,
        string? order = null,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default)
    {
        var url = BuildRestUrl(table, select, filters, order, limit, offset);

        _logger.LogDebug("Supabase GET: {Url}", url);

        var response = await _http.GetAsync(url, cancellationToken);
        await EnsureSuccess(response, "GET", table);

        return await response.Content.ReadFromJsonAsync<List<T>>(JsonOptions, cancellationToken) ?? [];
    }

    /// <summary>
    /// GET a single row by primary key.
    /// </summary>
    public async Task<T?> GetByIdAsync<T>(
        string table,
        string id,
        string idColumn = "id",
        string? select = null,
        CancellationToken cancellationToken = default)
    {
        var filters = new[] { $"{idColumn}=eq.{id}" };
        var url = BuildRestUrl(table, select, filters);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Accept", "application/vnd.pgrst.object+json");

        _logger.LogDebug("Supabase GET by ID: {Url}", url);

        var response = await _http.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotAcceptable)
            return default; // No row found

        await EnsureSuccess(response, "GET", table);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    /// <summary>
    /// INSERT a row and return the created record.
    /// </summary>
    public async Task<T> InsertAsync<T>(
        string table,
        object data,
        CancellationToken cancellationToken = default)
    {
        var url = $"/rest/v1/{table}";
        var json = JsonSerializer.Serialize(data, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };
        request.Headers.Add("Prefer", "return=representation");
        request.Headers.Add("Accept", "application/vnd.pgrst.object+json");

        _logger.LogDebug("Supabase INSERT into {Table}", table);

        var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, "INSERT", table);

        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken))!;
    }

    /// <summary>
    /// UPDATE a row by primary key and return the updated record.
    /// </summary>
    public async Task<T> UpdateAsync<T>(
        string table,
        string id,
        object data,
        string idColumn = "id",
        CancellationToken cancellationToken = default,
        JsonSerializerOptions? serializerOptions = null)
    {
        var url = $"/rest/v1/{table}?{idColumn}=eq.{id}";
        var json = JsonSerializer.Serialize(data, serializerOptions ?? JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = content
        };
        request.Headers.Add("Prefer", "return=representation");
        request.Headers.Add("Accept", "application/vnd.pgrst.object+json");

        _logger.LogDebug("Supabase UPDATE {Table} id={Id}", table, id);

        var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, "UPDATE", table);

        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken))!;
    }

    /// <summary>
    /// UPDATE rows matching arbitrary filters and return the updated records.
    /// </summary>
    public async Task<List<T>> UpdateWhereAsync<T>(
        string table,
        IEnumerable<string> filters,
        object data,
        CancellationToken cancellationToken = default)
    {
        var url = BuildRestUrl(table, filters: filters);
        var json = JsonSerializer.Serialize(data, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Patch, url)
        {
            Content = content
        };
        request.Headers.Add("Prefer", "return=representation");

        _logger.LogDebug("Supabase UPDATE WHERE on {Table}", table);

        var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, "UPDATE", table);

        return await response.Content.ReadFromJsonAsync<List<T>>(JsonOptions, cancellationToken) ?? [];
    }

    /// <summary>
    /// DELETE a row by primary key.
    /// </summary>
    public async Task DeleteAsync(
        string table,
        string id,
        string idColumn = "id",
        CancellationToken cancellationToken = default)
    {
        var url = $"/rest/v1/{table}?{idColumn}=eq.{id}";

        _logger.LogDebug("Supabase DELETE from {Table} id={Id}", table, id);

        var response = await _http.DeleteAsync(url, cancellationToken);
        await EnsureSuccess(response, "DELETE", table);
    }

    /// <summary>
    /// Call a PostgreSQL RPC function.
    /// </summary>
    public async Task<T?> RpcAsync<T>(
        string functionName,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"/rest/v1/rpc/{functionName}";
        var json = parameters != null ? JsonSerializer.Serialize(parameters, JsonOptions) : "{}";
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogDebug("Supabase RPC: {Function}", functionName);

        var response = await _http.PostAsync(url, content, cancellationToken);
        await EnsureSuccess(response, "RPC", functionName);

        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
    }

    // ==================================================================================
    // Storage API Methods
    // ==================================================================================

    /// <summary>
    /// Upload a file to Supabase Storage.
    /// </summary>
    /// <param name="bucket">Storage bucket name</param>
    /// <param name="path">File path within the bucket (e.g. "case-id/filename.pdf")</param>
    /// <param name="fileStream">File content stream</param>
    /// <param name="contentType">MIME type (e.g. "application/pdf")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The storage path of the uploaded file</returns>
    public async Task<string> UploadFileAsync(
        string bucket,
        string path,
        Stream fileStream,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var url = $"/storage/v1/object/{bucket}/{path}";
        var content = new StreamContent(fileStream);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content
        };
        // Upsert to overwrite existing files
        request.Headers.Add("x-upsert", "true");

        _logger.LogDebug("Supabase Storage UPLOAD: {Bucket}/{Path}", bucket, path);

        var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccess(response, "UPLOAD", $"{bucket}/{path}");

        return path;
    }

    /// <summary>
    /// Create a signed URL for downloading a file.
    /// </summary>
    /// <param name="bucket">Storage bucket name</param>
    /// <param name="path">File path within the bucket</param>
    /// <param name="expiresInSeconds">URL expiry time in seconds</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Signed download URL</returns>
    public async Task<string> GetSignedUrlAsync(
        string bucket,
        string path,
        int expiresInSeconds = 3600,
        CancellationToken cancellationToken = default)
    {
        var url = $"/storage/v1/object/sign/{bucket}/{path}";
        var body = new { expiresIn = expiresInSeconds };
        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var response = await _http.PostAsync(url, content, cancellationToken);
        await EnsureSuccess(response, "SIGN", $"{bucket}/{path}");

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var signedUrl = result.GetProperty("signedURL").GetString()!;

        // Return full URL
        return $"{_options.Url.TrimEnd('/')}{signedUrl}";
    }

    /// <summary>
    /// Delete a file from Supabase Storage.
    /// </summary>
    public async Task DeleteFileAsync(
        string bucket,
        string path,
        CancellationToken cancellationToken = default)
    {
        var url = $"/storage/v1/object/{bucket}/{path}";

        _logger.LogDebug("Supabase Storage DELETE: {Bucket}/{Path}", bucket, path);

        var response = await _http.DeleteAsync(url, cancellationToken);
        await EnsureSuccess(response, "DELETE FILE", $"{bucket}/{path}");
    }

    // ==================================================================================
    // Helpers
    // ==================================================================================

    private static string BuildRestUrl(
        string table,
        string? select = null,
        IEnumerable<string>? filters = null,
        string? order = null,
        int? limit = null,
        int? offset = null)
    {
        var sb = new StringBuilder($"/rest/v1/{table}");
        var sep = '?';

        if (!string.IsNullOrEmpty(select))
        {
            sb.Append(sep).Append("select=").Append(Uri.EscapeDataString(select));
            sep = '&';
        }

        if (filters != null)
        {
            foreach (var filter in filters)
            {
                // Filters are in PostgREST format: "column=operator.value"
                // Encode the value portion to handle special characters
                var eqIndex = filter.IndexOf('=');
                if (eqIndex > 0)
                {
                    var column = filter[..eqIndex];
                    var operatorAndValue = filter[(eqIndex + 1)..];
                    sb.Append(sep).Append(Uri.EscapeDataString(column))
                      .Append('=').Append(Uri.EscapeDataString(operatorAndValue));
                }
                else
                {
                    sb.Append(sep).Append(Uri.EscapeDataString(filter));
                }
                sep = '&';
            }
        }

        if (!string.IsNullOrEmpty(order))
        {
            sb.Append(sep).Append("order=").Append(Uri.EscapeDataString(order));
            sep = '&';
        }

        if (limit.HasValue)
        {
            sb.Append(sep).Append("limit=").Append(limit.Value);
            sep = '&';
        }

        if (offset.HasValue)
        {
            sb.Append(sep).Append("offset=").Append(offset.Value);
        }

        return sb.ToString();
    }

    private async Task EnsureSuccess(HttpResponseMessage response, string operation, string target)
    {
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogError(
                "Supabase {Operation} on {Target} failed: {StatusCode} — {Body}",
                operation, target, (int)response.StatusCode, body);

            throw new HttpRequestException(
                $"Supabase {operation} on {target} failed ({(int)response.StatusCode}): {body}",
                null,
                response.StatusCode);
        }
    }
}
