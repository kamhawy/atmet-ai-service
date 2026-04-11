# ATMET AI Assistant (Foundry) — roadmap

Philosophy: **AI assists, human decides** (HITL). The **ATMET AI Assistant** uses **Azure AI Foundry** workflow agents with the **Project Responses** API (`CreateResponseAsync`, `previous_response_id` for resume), portal state in **Supabase**, and optional **HTTP tools** for the Foundry agent hitting this API’s internal read routes. **Workflow and portal agent identifiers are configuration-driven**; behavior is grounded in entity/service/workflow data from the API and knowledge sources—not a hard-coded domain.

---

## Wave overview

| Wave | Goal | Status (as of this doc) |
|------|------|---------------------------|
| **0** | Prove Project OpenAI client + agent + conversation + pause/resume (`previous_response_id`) in a console spike | Done — `ATMET.AI.WorkflowSpike`, aligned config with `AzureAI` |
| **1** | Schema + contracts for conversation session + core DTOs / services | Done — Supabase migration on `conversations`, `FoundryConversationSessionPatch`, `UpdateFoundrySessionAsync`, `IPortalAiWorkflowService` + models |
| **2** | Internal HTTP read surface for Foundry-configured tools (entity-scoped) | Done — `IFoundryAgentReadService`, `/api/v1/internal/foundry/*`, `CaseDetailForAgent` / `ServiceDetailForAgent` |
| **3** | Server orchestration: workflow service + portal chat integration + persist session after each response | **Done** — workflow/resume wiring, response status mapping, pause clear on PATCH, SSE polish, operator notes (`OPERATIONS.md`) |
| **4** | HITL UX, Foundry HTTP tools, observability, tests, frontend portal evolution | **Done** — see [Wave 4](#wave-4-pilot--hardening) |
| **5** | Hybrid workflow, pause contracts, hardening, observability follow-through | **In progress** — OTel + PDF pause envelope + [staging checklist](./STAGING-CHECKLIST.md) done; **execution backlog:** hybrid path, keys/network, PII audit, live staging E2E ([Wave 5 backlog](#wave-5-backlog-and-next-steps)) |

---

## Implementation snapshot (latest)

| Area | What shipped |
|------|----------------|
| **Observability** | `ApplicationInsightsMonitorOptions` binds **`ApplicationInsights`** (`ConnectionString`, `EnableAdaptiveSampling`, `EnableDependencyTracking`, `EnablePerformanceCounterCollectionModule`). **`ObservabilityExtensions`**: OpenTelemetry + **`AddSource(ATMET.AI.PortalWorkflow)`**; full **`UseAzureMonitor()`** when dependency tracking is on, otherwise trace-only (ASP.NET + custom source + Azure trace exporter, optional metrics). **`TelemetryClient`** for **`TrackException`**. No registration when **`ASPNETCORE_ENVIRONMENT=Testing`** or missing connection string. |
| **Pause / mapping** | `PortalAiWorkflowResponseMapper` (status + text + tool **`OutputItems`**); **`WorkflowPauseEnvelopeParser`** (PDF §7 + legacy); persist **`pause_envelope`** jsonb + denormalized columns. |
| **Portal (web)** | **`WorkflowHitlBanner`**, **`workflow_resume`** message type + **`sendPortalAiWorkflowResume`** in **`usePortalChat`** / **`MessageRenderer`**, SSE cache refresh patterns. |
| **Tests / CI** | **`ATMET.AI.Service.sln`** Release test + publish **`--no-build`**; integration host **`appsettings.Integration.json`** + **`IntegrationTestConfig`**. |
| **Docs** | [DASHBOARDS-AND-KQL.md](./DASHBOARDS-AND-KQL.md), [OPERATIONS.md](./OPERATIONS.md), [TECHNICAL-REFERENCE.md](./TECHNICAL-REFERENCE.md), [STAGING-CHECKLIST.md](./STAGING-CHECKLIST.md). |

---

## Validation — completed waves (residuals)

Cross-check after Wave 4 UI/telemetry work; no open implementation gaps unless noted.

| Wave | Validated | Open / backlog |
|------|-----------|----------------|
| **0** | `ATMET.AI.WorkflowSpike` builds; config aligns with `AzureAI` | None |
| **1** | Supabase `conversations` Foundry columns; Core contracts + `PortalConversationResponse`; `UpdateFoundrySessionAsync` | Regenerate `atmet-ai-web` `types.ts` when migrations change |
| **2** | `IFoundryAgentReadService` + internal HTTP routes; entity header enforcement; tests | Scoped keys / network — [OPERATIONS.md](./OPERATIONS.md#scoped-api-keys-for-foundry-tools); PII review on `CaseDetailForAgent` |
| **3** | Workflow + resume SSE path; pause clear on PATCH; mapper status + text envelopes | Hybrid workflow — [Wave 5 backlog](#wave-5-backlog-and-next-steps); pause contract — [OPERATIONS.md](./OPERATIONS.md) |
| **4** | Logging dimensions; `ActivitySource`; tool `OutputItems` pause merge; integration test keys; portal HITL banner + `workflow_resume`; CI tests solution | **Done** — OTel + `UseAzureMonitor` in API (`ObservabilityExtensions.cs`); [DASHBOARDS-AND-KQL.md](./DASHBOARDS-AND-KQL.md) |

---

## Wave 0 (spike)

**Deliverables**

- Console project `src/tools/ATMET.AI.WorkflowSpike`: `AIProjectClient` → `ProjectOpenAIClient` → `CreateProjectConversationAsync` → `GetProjectResponsesClientForAgent` → three linked `CreateResponseAsync` turns (overload + `CreateResponseOptions`).
- Configuration discovery from `ATMET.AI.Api/appsettings*.json` (`AzureAI`) plus env (`AzureAI__*`).

**Non-goals**

- Not deployed with the API; no production runtime impact unless the tool is run deliberately.

---

## Wave 1 (schema and contracts)

**Deliverables**

- Supabase: `conversations` columns — `foundry_project_conversation_id`, `foundry_run_id`, `last_response_id`, `pause_ui_action`, `pause_waiting_for`, `foundry_current_step`, `conversation_language` (see `atmet-ai-web/supabase/migrations/`).
- Core: `IPortalAiWorkflowService`, `PortalAiWorkflow*` models, `FoundryConversationSessionPatch`, extended `PortalConversationResponse`.
- Infrastructure: `PortalConversationService` select/map/update for Foundry fields.

**Follow-up**

- Regenerate web `types.ts` when migrations change (portal repo).

---

## Wave 2 (internal Foundry tool reads)

**Deliverables**

- `IFoundryAgentReadService` / `FoundryAgentReadService`: in-process Supabase reads with `entity_id` enforcement.
- HTTP: `GET /api/v1/internal/foundry/cases/{caseId}`, `.../by-reference/{referenceNumber}`, `.../services/{serviceId}` — same API key as `/api/v1`, header `X-Portal-Entity-Id`.
- `IPortalWorkflowService.GetWorkflowStateForEntityAsync` for workflow snapshot without impersonating a citizen user id.

**Principles**

- Browser / SPA must **not** depend on these routes; they exist for **Foundry agent HTTP tools** (server-side URL + key) and mirror what `PortalAiWorkflowService` already loads via `IFoundryAgentReadService`.

**Hardening backlog**

- Scoped API keys or private networking for tool traffic; PII exposure (`requesterUserId`) review for agent payloads.

---

## Wave 3 (orchestration)

**Delivered**

- `PortalAiWorkflowService`: tenant verification (`id` + `user_id` + `entity_id`), PDF-aligned **turn JSON** (`user_message`, `thread_state`, `attachments`, optional `resume_payload`) to Foundry, create/reuse Foundry project conversation, chain `previous_response_id` only when a stored project conversation exists (or on explicit resume id), `UpdateFoundrySessionAsync` after each response.
- `PortalAiWorkflowResponseMapper`: maps `ResponseResult.Status` (reflection), JSON assistant envelopes, and tool **`OutputItems`** strings into `PortalAiWorkflowTurnResult` and pause DB fields.
- `PortalAgentService`: workflow-only — single `typing` event, `StartOrContinueAsync` or `ResumeAsync` for **`type`:** `workflow_resume`, **`error`** then **`done`** on failure (no Persistent Agents chat loop).
- `FoundryConversationSessionPatch.ClearPauseFields` + PostgREST PATCH with explicit JSON nulls (`SupabaseRestClient.JsonOptionsIncludeNulls`) to clear `pause_*` and **`pause_envelope`** after non-pause turns.

**Follow-up**

- See [Wave 5 backlog and next steps](#wave-5-backlog-and-next-steps) (hybrid path, pause envelope contract, hardening).

---

## Wave 4 (pilot + hardening)

**Goal:** production pilot readiness — observability, operator docs for Foundry HTTP tools, automated tests, then HITL UX on web and deeper pause semantics.

| Track | Status |
|-------|--------|
| Structured logging (workflow + portal SSE path) | Done — `Atmet*` log dimensions; duration and chaining flags on workflow turn |
| Foundry HTTP tools operator doc | Done — [FOUNDRY-HTTP-TOOLS.md](./FOUNDRY-HTTP-TOOLS.md) |
| Unit tests (tenant header contract) | Done — `src/ATMET.AI.Api.Tests` |
| Integration tests (internal routes + `WebApplicationFactory`) | Done — fake `IFoundryAgentReadService`; 400 / 401 / 404 / 200 coverage |
| CI runs tests before publish | Done — GitHub Actions `dotnet test ATMET.AI.Service.sln` (Release), then `dotnet publish --no-build` |
| Custom `ActivitySource` / distributed spans for Foundry calls | Done — `PortalAiWorkflowTelemetry.Source` (`ATMET.AI.PortalWorkflow`) + `ObservabilityExtensions` (`AddOpenTelemetry` / `UseAzureMonitor` / `AddSource`) |
| Pause hints from non-text tool outputs | Done — `PortalAiWorkflowResponseMapper` scans `ResponseResult.OutputItems` tool-like items for JSON; shared parser in `WorkflowPauseEnvelopeParser` |
| Frontend — `workflow_resume` UX after pause | Done — MUBASHIR `usePortalChat` + `ChatInterface` / `WorkflowHitlBanner`; `MessageRenderer` bilingual copy when resume has no user text |
| Scoped API keys / network restriction for tool traffic | Doc — [OPERATIONS.md](./OPERATIONS.md#scoped-api-keys-for-foundry-tools) (operational backlog; Wave 2 hardening) |

---

## Wave 5 backlog and next steps

**Goal:** production confidence and product depth for the **Foundry workflow + MUBASHIR HITL** path — **Foundry + portal scope only** (see [Deferred](#deferred--supabase-crud-via-backend-api) for Supabase CRUD via backend API).

| Track | Status | Notes |
|-------|--------|--------|
| **OpenTelemetry + App Insights config** | **Done** | `ObservabilityExtensions` + `ApplicationInsightsMonitorOptions`; `Testing` / empty connection string skips registration |
| **Dashboards / SLOs** | **Doc done** | [DASHBOARDS-AND-KQL.md](./DASHBOARDS-AND-KQL.md) — ops: create workbook tiles and alerts |
| **Pause / tool contracts** | **Done (API)** | **`WorkflowPauseEnvelopeParser`** + **`pause_envelope`** column; unit tests in `WorkflowPauseEnvelopeParserTests`; agent prompt alignment is operational |
| **Hybrid workflow** | **Backlog** | **Next:** product picks journeys; design orchestration (Project Responses + selective in-API or Foundry-side tools); spike smallest vertical |
| **Hardening (keys / network)** | **Backlog** | **Next:** second key + APIM path restriction per [OPERATIONS.md](./OPERATIONS.md#scoped-api-keys-for-foundry-tools); optional private egress |
| **PII / agent payloads** | **Backlog** | **Next:** audit `CaseDetailForAgent` / `ServiceDetailForAgent`; trim or redact fields in tool JSON |
| **Staging E2E** | **Doc done; run backlog** | [STAGING-CHECKLIST.md](./STAGING-CHECKLIST.md) — verify **`WorkflowAgentName`** / version, execute pause → Continue → resume, optional HTTP tools; Wave 5 **closes** for Foundry-only scope once this run is signed off |

---

## Next steps (ordered — this program only)

1. **Staging E2E (execute)** — Follow [STAGING-CHECKLIST.md](./STAGING-CHECKLIST.md): confirm workflow agent config, pause → Continue → resume, optional Foundry HTTP tools.
2. **Ops** — Confirm traces and log queries in Azure Monitor using [DASHBOARDS-AND-KQL.md](./DASHBOARDS-AND-KQL.md); set alerts on failed workflow turns / latency (e.g. from `Atmet*` log properties).
3. **Hardening** — Scoped API keys (or APIM) for `/api/v1/internal/foundry/*`; document rotation in your runbook ([OPERATIONS.md](./OPERATIONS.md)).
4. **Hybrid workflow** — Design and implement minimal hybrid path if product requires Persistent tools alongside Project Responses.
5. **PII** — Review and reduce agent-facing case/service fields.

---

## After Wave 5 (same program, not a numbered “Wave 6” doc)

These items remain **until executed**; they are **not** blocked by Supabase CRUD migration ([Deferred](#deferred--supabase-crud-via-backend-api)).

| Item | Type |
|------|------|
| Hybrid workflow (workflow + selective portal tools) | Engineering + product |
| Scoped keys / APIM for internal Foundry routes | Security / ops |
| PII trim on `CaseDetailForAgent` / `ServiceDetailForAgent` | Engineering |
| Live staging / prod sign-off on checklist | Ops |

There is **no separate Wave 6** in this roadmap file; the table above is the continuation after Wave 5 execution sign-off.

---

<a id="deferred--supabase-crud-via-backend-api"></a>

## Postponed — Supabase CRUD via backend API (platform track)

**Status:** **Postponed** (April 2026). Migrating *all* portal/web data access from direct Supabase PostgREST to **`atmet-ai-service`** REST CRUD is **not** in active delivery. The API already uses Supabase server-side for portal orchestration (e.g. conversations, cases, documents). The full table-by-table specification for a future program phase remains in archived [`BACKEND_API_PROMPT.md`](../../../atmet-ai-web/docs/archive/BACKEND_API_PROMPT.md) (read the file’s **Status** banner). Repo-level context: **[`CLAUDE.md`](../../../CLAUDE.md)**.

**Out of scope for this Foundry / portal AI assistant roadmap:** the above migration does **not** block Wave 5 execution items, hybrid workflow design, or staging sign-off.

---

## Dependencies

- **Azure**: Foundry project endpoint, managed identity (or dev credential), deployed workflow agent name/version (`AzureAI:WorkflowAgentName` / `WorkflowAgentVersion`).
- **Supabase**: service role for API; RLS not relied on by service (server uses elevated key).
- **Frontend**: continues to use `POST /api/v1/portal/conversations/{id}/chat` for chat; no requirement to call internal Foundry routes.

---

## Risks and mitigations

| Risk | Mitigation |
|------|------------|
| Portal chat is workflow-only (Persistent Agents loop removed) | Workflow agent must cover needed behaviors; hybrid is backlog if product requires it |
| Internal reads + shared API key | Least-privilege keys, network restriction, future signed tool tokens |
| Stale `last_response_id` | Current code only chains when `foundry_project_conversation_id` already exists |
| GDPR / PII in agent payloads | Minimize fields in tool responses; audit `CaseDetailForAgent` |
