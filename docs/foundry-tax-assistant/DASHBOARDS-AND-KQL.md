# ATMET AI Assistant — Application Insights dashboards and KQL

The API exports **OpenTelemetry traces** to Azure Monitor when `ApplicationInsights:ConnectionString` is set (see `ATMET.AI.Api.Extensions.ObservabilityExtensions`). Portal Foundry workflow turns emit spans from **`ATMET.AI.PortalWorkflow`** with operation name **`PortalWorkflow.ExecuteTurn`**.

Structured logs use Serilog dimensions such as **`AtmetConversationId`**, **`AtmetWorkflowStatus`**, **`AtmetWorkflowDurationMs`** (see [TECHNICAL-REFERENCE.md](./TECHNICAL-REFERENCE.md)).

---

## Trace and log queries (Logs → query)

Use **Transaction search** in the Azure portal for operation names containing **`PortalWorkflow`**, or run KQL in **Logs**. Schema varies slightly by ingestion version; start with **`traces`** and **`requests`**.

**Log lines mentioning the workflow turn**

```kusto
traces
| where timestamp > ago(24h)
| where message has "Portal AI workflow turn" or message has "PortalWorkflow"
| project timestamp, severityLevel, message, customDimensions
| order by timestamp desc
| take 100
```

**Filter by conversation id** (replace GUID)

```kusto
traces
| where timestamp > ago(24h)
| where customDimensions.AtmetConversationId == "YOUR-CONVERSATION-GUID"
| project timestamp, message, severityLevel, customDimensions
| order by timestamp desc
```

---

## Suggested workbook tiles

| Tile                                         | Source                       | Purpose             |
| -------------------------------------------- | ---------------------------- | ------------------- |
| Workflow turn count                          | `traces` / custom dimensions | Volume per hour     |
| `AtmetWorkflowStatus` = `paused_for_hitl`    | Logs                         | HITL backlog signal |
| P95 `AtmetWorkflowDurationMs`                | Logs → summarize             | Latency SLO         |
| Failed requests to `/api/v1/portal/.../chat` | `requests`                   | Portal SSE health   |

---

## Notes

- **Testing** environment disables OTel registration so `WebApplicationFactory` tests do not emit to Azure.
- **Hybrid workflow** and **backend Supabase CRUD** remain program-sized work; see [ROADMAP.md](./ROADMAP.md) Wave 5 table.
