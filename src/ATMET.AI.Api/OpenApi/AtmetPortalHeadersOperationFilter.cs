using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ATMET.AI.Api.OpenApi;

/// <summary>
/// Ensures portal context headers appear in OpenAPI for integrators. Minimal APIs do not always emit header parameters from binding metadata.
/// </summary>
public sealed class AtmetPortalHeadersOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var path = context.ApiDescription.RelativePath?.TrimStart('/') ?? "";
        var method = context.ApiDescription.HttpMethod;

        if (!path.StartsWith("api/v1/portal/", StringComparison.OrdinalIgnoreCase))
            return;

        if (path.Equals("api/v1/portal/services", StringComparison.OrdinalIgnoreCase) && string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            AddHeaderIfMissing(operation, "X-Portal-Entity-Id", """
                **Tenant scope** — UUID of the government entity (organization) whose service catalog should be returned.

                Must match a row in the `entities` table used by the portal backend.
                """,
                required: true);
            return;
        }

        if (IsAnonymousServiceCatalogDetail(path, method))
            return;

        AddHeaderIfMissing(operation, "X-Portal-User-Id", """
            **End-user identity** — UUID of the signed-in citizen/profile (`auth.users.id` / `profiles.id` in Supabase).

            Used to scope cases, conversations, documents, and mutations to that user.
            """,
            required: true);

        if (RequiresEntityHeader(path, method))
        {
            AddHeaderIfMissing(operation, "X-Portal-Entity-Id", """
                **Tenant scope** — UUID of the entity (organization) for multi-tenant isolation.

                Required when listing cases or conversations and for portal chat so the agent loads the correct catalog and policies.
                """,
                required: true);
        }

        if (path.Contains("/chat", StringComparison.OrdinalIgnoreCase))
        {
            AddHeaderIfMissing(operation, "X-Portal-Language", """
                **UI / agent locale** — `en` or `ar` (default `en` if omitted).

                Influences bilingual copy and agent responses where applicable.
                """,
                required: false);
        }
    }

    /// <summary>
    /// Service detail and workflow endpoints intentionally bind only route parameters (no portal headers).
    /// </summary>
    private static bool IsAnonymousServiceCatalogDetail(string path, string? method)
    {
        if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!path.StartsWith("api/v1/portal/services/", StringComparison.OrdinalIgnoreCase))
            return false;

        // api/v1/portal/services/{serviceId}[/workflow]
        var suffix = path["api/v1/portal/services/".Length..];
        return suffix.Length > 0;
    }

    /// <summary>
    /// Routes that list or stream data across an entity boundary need the entity header.
    /// </summary>
    private static bool RequiresEntityHeader(string path, string? method)
    {
        if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) &&
            path.Equals("api/v1/portal/cases", StringComparison.OrdinalIgnoreCase))
            return true;

        if (path.StartsWith("api/v1/portal/conversations", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) &&
            path.Equals("api/v1/portal/conversations", StringComparison.OrdinalIgnoreCase))
            return true;

        if (path.Contains("/chat", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static void AddHeaderIfMissing(OpenApiOperation operation, string name, string description, bool required)
    {
        operation.Parameters ??= [];

        foreach (var p in operation.Parameters)
        {
            if (p is not null && string.Equals(p.Name, name, StringComparison.Ordinal) && p.In == ParameterLocation.Header)
                return;
        }

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = name,
            In = ParameterLocation.Header,
            Required = required,
            Description = description.Trim(),
            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
        });
    }
}
