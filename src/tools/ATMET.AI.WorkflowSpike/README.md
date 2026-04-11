# ATMET.AI.WorkflowSpike

Wave 0 harness: calls your Azure AI Foundry **workflow agent** via `Azure.AI.Projects` 2.0 + `Azure.AI.Extensions.OpenAI` 2.0 (`ProjectOpenAIClient`, `ProjectResponsesClient`).

## Prerequisites

- .NET 10 SDK
- Azure credentials (`az login` or environment configured for `DefaultAzureCredential`)
- Foundry project endpoint and deployed agent name/version

## Run

```powershell
cd atmet-ai-service
# Preferred: same keys as ATMET.AI.Api (appsettings.json → AzureAI, overridable in Azure as AzureAI__*)
# $env:AzureAI__ProjectEndpoint = "https://<resource>.services.ai.azure.com/api/projects/<projectId>"
# $env:AzureAI__WorkflowAgentName = "tax-wf"
# $env:AzureAI__WorkflowAgentVersion = "7"
dotnet run --project src/tools/ATMET.AI.WorkflowSpike -- "Hello"
```

The tool loads `src/ATMET.AI.Api/appsettings.json` and `appsettings.{DOTNET_ENVIRONMENT|ASPNETCORE_ENVIRONMENT|Production}.json` when it can find that folder (walks up from cwd or from the build output directory). Override with **`ATMET_APPSETTINGS_DIR`** pointing at the Api project folder if needed.

The harness runs **three** linked turns: first message from args, then a fixed follow-up using `previousResponseId`, then a third turn using `CreateResponseOptions.PreviousResponseId`.

## Next steps (implementation plan)

- Wire **`IPortalAiWorkflowService`** to persist `response.Id` as `last_response_id` and pass it on resume (same pattern as this spike).
- Replace ad-hoc `form_data._agent_thread_id` with `foundry_project_conversation_id` on `conversations` (see Supabase migration in `atmet-ai-web/supabase/migrations/`).
