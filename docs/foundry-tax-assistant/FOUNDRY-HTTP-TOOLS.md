# Foundry HTTP tools (Wave 4)

Configure **server-side HTTP tools** on your Foundry workflow agent so they call this API’s internal read routes. Browsers must not use these URLs; the agent holds the API key.

---

## Base URL

Use the deployed API origin, for example:

`https://<your-api-host>/api/v1/internal/foundry`

---

## Authentication and tenant

| Item | Value |
|------|--------|
| Header | `X-Api-Key: <same key as /api/v1>` |
| Header | `X-Portal-Entity-Id: <government entity UUID>` — required; must match the case or service row’s `entity_id` |

Missing `X-Portal-Entity-Id` returns **400** with `{ "error": "X-Portal-Entity-Id header is required." }`.

Wrong entity for an existing resource returns **404** (no cross-tenant leakage).

---

## Routes (read-only)

| Method | Path | Use |
|--------|------|-----|
| GET | `/cases/{caseId}` | Case + workflow snapshot + linked conversations (Foundry session fields) |
| GET | `/cases/by-reference/{referenceNumber}` | Same payload keyed by citizen reference number |
| GET | `/services/{serviceId}` | Service definition, form schema, workflow binding |

OpenAPI tag: **Internal - Foundry tools**.

---

## Example (curl)

```bash
curl -sS \
  -H "X-Api-Key: $ATMET_API_KEY" \
  -H "X-Portal-Entity-Id: $ENTITY_ID" \
  "https://<host>/api/v1/internal/foundry/cases/<caseId>"
```

---

## Operational notes

- Prefer **scoped or secondary API keys** for agent-only traffic when your platform supports it (see roadmap hardening).
- Ensure the agent prompt does not echo raw PII from tool JSON into untrusted channels.
- **`PortalAiWorkflowService`** already loads the same data in-process via **`IFoundryAgentReadService`**; HTTP tools are for the Foundry agent runtime, not for the React SPA.

---

## Related

- [TECHNICAL-REFERENCE.md](./TECHNICAL-REFERENCE.md) — types and security model  
- [ROADMAP.md](./ROADMAP.md) — Wave 4 status  
- [OPERATIONS.md](./OPERATIONS.md) — workflow-only portal chat and operator cleanup  
