# Staging checklist — portal AI workflow + HITL

Use this before promoting workflow + pause/resume behavior beyond dev. **Supabase** and **Foundry** (including **`WorkflowAgentName`** / **`WorkflowAgentVersion`**) must match the environment you are validating.

## 1. Configuration (API)

In staging `appsettings` or environment overrides:

| Setting | Staging recommendation |
|---------|-------------------------|
| `AzureAI:WorkflowAgentName` / `WorkflowAgentVersion` | **Required** — must match the deployed Foundry workflow agent (`IPortalAiWorkflowService` / Project Responses). |
| `ApplicationInsights:ConnectionString` | Set so workflow turns appear in Monitor (see [DASHBOARDS-AND-KQL.md](./DASHBOARDS-AND-KQL.md)). |

Frontend must point `VITE_ATMET_AI_API_URL` (and API key) at this API instance.

## 2. Foundry agent / workflow

- Workflow agent should emit pause payloads aligned with the Tax Assistant PDF §7 (see [TECHNICAL-REFERENCE.md](./TECHNICAL-REFERENCE.md)).
- **Resume:** client sends `workflow_resume` with `previous_response_id` = `last_response_id` from the conversation row (see portal `usePortalChat`).

## 3. E2E — pause → Continue → resume

1. Open MUBASHIR chat for a conversation with service/case context as required.
2. Drive the agent to a **paused** state (HITL). Confirm **`WorkflowHitlBanner`** shows and DB has `pause_ui_action` / `pause_waiting_for` / `last_response_id` populated.
3. Click **Continue** (or equivalent). Confirm a user-visible resume occurs and the next assistant reply streams.
4. Confirm non-pause turns **clear** pause columns (`ClearPauseFields` / explicit null PATCH).

## 4. Optional — Foundry HTTP tools → internal routes

If the agent uses HTTP tools to this API:

- Base URL: your API origin + `/api/v1/internal/foundry/...` (see [FOUNDRY-HTTP-TOOLS.md](./FOUNDRY-HTTP-TOOLS.md)).
- Same **`X-Api-Key`** as `/api/v1`, plus **`X-Portal-Entity-Id`** matching the citizen’s entity.
- Smoke-test `GET .../cases/{id}` or `.../services/{id}` from the tool configuration or curl with staging keys.

## 5. Observability

- After a few turns, run the KQL samples in [DASHBOARDS-AND-KQL.md](./DASHBOARDS-AND-KQL.md) for `PortalWorkflow.ExecuteTurn` and workflow log dimensions.

## 6. Rollback / hotfix

There is **no** configuration switch to restore the removed Persistent Agents chat loop. Mitigations: fix **`WorkflowAgentName`** / **`WorkflowAgentVersion`** and Foundry-side behavior, or (only if your registry still has one) deploy an **older API build** that predates workflow-only portal chat. Remove stale App Service keys **`AzureAI__UsePortalAiWorkflow`** / **`AzureAI__UseTaxAssistantWorkflow`** — they are unused.
