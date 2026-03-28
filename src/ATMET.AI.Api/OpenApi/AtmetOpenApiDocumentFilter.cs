using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ATMET.AI.Api.OpenApi;

/// <summary>
/// Enriches the generated OpenAPI document with tag descriptions, default server entry, and ordered tag metadata for integrators.
/// </summary>
public sealed class AtmetOpenApiDocumentFilter : IDocumentFilter
{
    private static readonly (string Name, string Description)[] TagDefinitions =
    [
        (
            "Agents",
            """
            **Azure AI Foundry — persistent agents** (threads, messages, runs, files).

            Use these endpoints when you need **multi-turn, stateful** agent conversations backed by the Azure AI Agents Persistent API: create agents, attach threads, post messages, execute runs, and upload files for tools (for example code interpreter or file search).

            **Typical flow:** `CreateAgent` → `CreateThread` → `AddMessage` (user) → `CreateRun` → poll `GetRun` until completed → `GetMessages` for the assistant reply.

            **Writes** (create/update/delete agents, threads, messages, runs, files) require the same API key with permission for mutating operations (`ApiWriter` policy in this service).
            """),
        (
            "Chat",
            """
            **Azure OpenAI chat completions** — stateless request/response and **SSE streaming**.

            Use `/chat/completions` for one-shot or multi-turn chat without persistent agent threads. Optional `model` falls back to the deployment configured for this API instance.

            **Streaming:** `POST /chat/completions/stream` returns `text/event-stream` (Server-Sent Events). Each line is `data: {json}` with partial chunks; the stream ends with `data: [DONE]`.
            """),
        (
            "Deployments",
            """
            **Model deployments** available in the connected Azure AI Foundry project (names, SKUs, capabilities).

            Use this to discover which **deployment names** to pass as `model` in chat or agent calls, and to validate capacity or publisher filters.
            """),
        (
            "Connections",
            """
            **Project connections** to Azure resources (for example Azure OpenAI, Azure AI Search).

            Lists connection metadata **without secrets**. Use connection names when creating datasets or search indexes that must target a specific linked resource.
            """),
        (
            "Datasets",
            """
            **Dataset versions** in the project (file or folder uploads, listing, credentials, delete).

            Uploads use **multipart/form-data**. Dataset credentials return **time-limited SAS** URIs for direct blob access (training pipelines, batch jobs). **Mutations** require `ApiWriter`.
            """),
        (
            "Indexes",
            """
            **Azure AI Search index** registrations in the project (create/update, list versions, delete).

            Registers or updates metadata that points the project at a search index via a named connection. **Mutations** require `ApiWriter`.
            """),
        (
            "Portal - Services",
            """
            **MUBASHIR / citizen portal — service catalog** (Supabase-backed).

            Returns active government **services** for an entity (tenant), optional **form schema**, and **workflow** definitions for a service. Responses are suitable for building service pickers and dynamic forms.

            **Caching:** catalog GET responses may be **output-cached** (short TTL) for performance.

            **Headers:** `GET /portal/services` requires **`X-Portal-Entity-Id`**. Service-by-id and workflow reads do not require portal context headers in the current implementation.
            """),
        (
            "Portal - Cases",
            """
            **Citizen cases (applications)** — create, list, detail, and status updates.

            A **case** ties a user to a service submission with workflow state, reference number, and JSON payloads (`submittedData`, `eligibilityResult`). Use **`X-Portal-User-Id`** and **`X-Portal-Entity-Id`** on list/detail; create and status updates require **`X-Portal-User-Id`**.

            **Query:** `GET /portal/cases` supports optional `status` filter.
            """),
        (
            "Portal - Conversations",
            """
            **Portal conversations and messages** (persisted chat threads).

            List or create conversations, fetch full thread with messages, delete threads, or append a message. Used together with **Portal Chat (SSE)** for AI-guided flows. Requires **`X-Portal-User-Id`**; list also requires **`X-Portal-Entity-Id`**.
            """),
        (
            "Portal - Documents",
            """
            **Case documents** — upload, list, download metadata with signed URL, and **checklist** progress.

            Upload uses **multipart/form-data** (`file`); optional `documentCatalogId` links the file to a catalog requirement. Checklist merges required documents with upload status for UX. Requires **`X-Portal-User-Id`** on all routes in this group.
            """),
        (
            "Portal - Forms",
            """
            **Dynamic forms** bound to a case — get schema and current data, partial save, server-side validation, and final submit.

            Form definitions come from the service **form schema**; payloads are JSON (`JsonElement`) for flexibility. Submit transitions the case according to business rules. Requires **`X-Portal-User-Id`**.
            """),
        (
            "Portal - Workflow",
            """
            **Workflow state** for a case — computed progress and **step completion**.

            `GET` returns steps, completion status, and progress percentage. `POST .../steps/{stepId}/complete` marks a step done (optional comment). Requires **`X-Portal-User-Id`**.
            """),
        (
            "Portal - Activity",
            """
            **Audit / activity timeline** for a case (who did what, status changes, payloads).

            Read-only; requires **`X-Portal-User-Id`** for authorization scoping to the case owner context.
            """),
        (
            "Portal Chat",
            """
            **AI agent streaming** for the portal — **Server-Sent Events** over `POST`.

            Sends a structured **`PortalChatMessage`** (discriminated by `type`). The agent may emit **tool_call** events while invoking server tools (create case, submit form, etc.), then **message** events with UI-oriented payloads (`service_catalog`, `form_request`, …). Stream ends with `data: [DONE]`.

            **Headers:** **`X-Portal-User-Id`**, **`X-Portal-Entity-Id`**, optional **`X-Portal-Language`** (`en` / `ar`).
            """),
        (
            "Info",
            """
            **Service discovery** — minimal JSON with version pointers and links to documentation and health checks. **Unauthenticated** (root only); not under `/api/v1`.
            """)
    ];

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        swaggerDoc.Info ??= new OpenApiInfo();

        swaggerDoc.Servers ??= [];
        if (swaggerDoc.Servers.Count == 0)
        {
            swaggerDoc.Servers.Add(new OpenApiServer
            {
                Url = "/",
                Description =
                    "Relative to the host serving this API. Replace with your deployment base URL (for example `https://api.example.com`) when generating clients."
            });
        }

        swaggerDoc.Tags ??= new HashSet<OpenApiTag>();

        var byName = swaggerDoc.Tags.Where(t => t.Name is not null).ToDictionary(t => t.Name!, StringComparer.Ordinal);

        foreach (var (name, description) in TagDefinitions)
        {
            if (byName.TryGetValue(name, out var existing))
            {
                existing.Description = description.Trim();
                continue;
            }

            var tag = new OpenApiTag { Name = name, Description = description.Trim() };
            swaggerDoc.Tags.Add(tag);
            byName[name] = tag;
        }
    }
}
