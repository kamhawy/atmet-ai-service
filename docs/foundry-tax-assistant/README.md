# ATMET AI Assistant (Foundry) — documentation index

This folder documents the **Microsoft Foundry–backed portal AI assistant**: Project Responses workflow orchestration, session persistence, optional internal HTTP tool reads, and HITL pause/resume. **Agent names, workflow name/version, and instructions are environment configuration**; orchestration is **entity-agnostic** and loads case/service/workflow context from Supabase and (when configured) knowledge indexes.

> **Note:** The directory name `foundry-tax-assistant` is legacy; content here applies to the generic **ATMET AI Assistant**, not a single vertical.

**Workspace-wide guide:** [`CLAUDE.md`](../../../CLAUDE.md) (repository root) — personas, portal AI ↔ Foundry wiring, postponed Supabase CRUD program, env vars, key file paths.

| Document | Purpose |
|----------|---------|
| [ROADMAP.md](./ROADMAP.md) | Wave-based roadmap, completed work, priorities, risks |
| [TECHNICAL-REFERENCE.md](./TECHNICAL-REFERENCE.md) | Configuration, endpoints, types, code paths |
| [OPERATIONS.md](./OPERATIONS.md) | Workflow-only portal chat, obsolete App Service keys, resume and session behavior |
| [STAGING-CHECKLIST.md](./STAGING-CHECKLIST.md) | Staging validation (workflow agent config, pause/resume, optional HTTP tools) |
| [FOUNDRY-HTTP-TOOLS.md](./FOUNDRY-HTTP-TOOLS.md) | Register Foundry agent HTTP tools (URLs, headers, curl) |

Related elsewhere in this repo:

- [FoundryWorkflowSample.txt](../FoundryWorkflowSample.txt) — minimal SDK snippet
- `src/tools/ATMET.AI.WorkflowSpike/README.md` — runnable Wave 0 harness
