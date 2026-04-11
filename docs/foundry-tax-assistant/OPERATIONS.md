# ATMET AI Assistant (Foundry) — operations

Notes for operators and release managers running portal AI against Foundry **Project Responses** and pause/resume.

---

## Portal chat is workflow-only (no feature flag)

`POST /api/v1/portal/conversations/{id}/chat` always uses **`IPortalAiWorkflowService`** (`PortalAiWorkflowService`). Older **`AzureAI:UsePortalAiWorkflow`** / **`UseTaxAssistantWorkflow`** settings are **not read** by current API builds.

**Azure App Service / environment cleanup:** delete obsolete application settings so configuration reviews stay accurate:

- `AzureAI__UsePortalAiWorkflow`
- `AzureAI__UseTaxAssistantWorkflow` (legacy rename)

**Required for portal chat:** **`AzureAI__WorkflowAgentName`** and **`AzureAI__WorkflowAgentVersion`** must match the deployed Foundry workflow agent. See also [DEPLOYMENT.md](../DEPLOYMENT.md) §7a.

---

## Pause envelopes (Tax Assistant PDF §7)

There is **no** embedded JSON Schema for pause payloads. **`WorkflowPauseEnvelopeParser`** (Core) merges **PDF-style** objects (`ui_action`, `message_to_user`, `required_fields`, `run_id`, …) and **legacy** `{ pause, uiAction, waitingFor }` from assistant text or tool **`OutputItems`**, normalizes to camelCase keys, and persists the result in Supabase **`conversations.pause_envelope`** (plus denormalized `pause_ui_action`, `pause_waiting_for`, `foundry_run_id`). String fields are capped at **`WorkflowPauseEnvelopeParser.MaxPauseFieldLength`** (2048).

---

## Product responsibilities (workflow-only)

The API does **not** run an in-process **Persistent Agents** function-tool loop for citizen SSE chat. Case/service behavior must come from the **workflow agent** (Foundry-side tools, HTTP tools to internal routes, or instructions that drive the citizen through portal UI), or from a future **hybrid** design if product requires additional orchestration.

Validate the workflow agent for the same journeys before production rollout.

---

## HITL resume

After a pause, the client sends **`type`:** `workflow_resume` with **`data.previousResponseId`** and optional **`data.resumePayload`** (see `PortalAiWorkflowResumeData` in Core and [TECHNICAL-REFERENCE.md](./TECHNICAL-REFERENCE.md)).

---

## Session columns

`conversations` stores `foundry_project_conversation_id`, `last_response_id`, pause hints, step, and language; non-pause turns clear `pause_*` via explicit JSON null PATCH.

---

## Scoped API keys (Foundry tools)

**Goal:** a dedicated API key (or App Registration) used only by Foundry-hosted HTTP tools, with rotation separate from the SPA key.

**Today:** the same `ApiKeys:Keys` list authorizes both the citizen BFF and internal Foundry routes (`X-Api-Key`).

**Recommended:** issue a second key, restrict it in your gateway / APIM to `/api/v1/internal/foundry/*` only, store it in Foundry tool secrets, and document rotation in your runbook. No extra code path is required in this repo once you add the key to configuration and Foundry.

---

## Azure Monitor (traces + logs)

OpenTelemetry export and **`TelemetryClient`** are wired when `ApplicationInsights:ConnectionString` is present (disabled in **`ASPNETCORE_ENVIRONMENT=Testing`**). Sample KQL and workbook ideas: [DASHBOARDS-AND-KQL.md](./DASHBOARDS-AND-KQL.md).

---

## Related

- [ROADMAP.md](./ROADMAP.md) — wave status  
- [STAGING-CHECKLIST.md](./STAGING-CHECKLIST.md) — workflow agent alignment, pause/resume E2E, optional internal HTTP tools  
- [TECHNICAL-REFERENCE.md](./TECHNICAL-REFERENCE.md) — configuration and flow  
- [DASHBOARDS-AND-KQL.md](./DASHBOARDS-AND-KQL.md) — Application Insights queries  
- [FOUNDRY-HTTP-TOOLS.md](./FOUNDRY-HTTP-TOOLS.md) — URLs and headers for Foundry agent HTTP tools  
