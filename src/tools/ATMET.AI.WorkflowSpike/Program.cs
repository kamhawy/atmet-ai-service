// Wave 0: Foundry workflow / Project Responses spike harness (Azure.AI.Projects 2.0 + Azure.AI.Extensions.OpenAI 2.0).
// Run (with Azure credentials, e.g. az login):
//   dotnet run --project src/tools/ATMET.AI.WorkflowSpike -- "Your first message"
//
// Configuration (same as running ATMET.AI.Api — see src/ATMET.AI.Api/appsettings*.json, section "AzureAI"):
//   AzureAI:ProjectEndpoint        — Foundry project URL (env: AzureAI__ProjectEndpoint)
//   AzureAI:WorkflowAgentName      — AgentReference name (env: AzureAI__WorkflowAgentName)
//   AzureAI:WorkflowAgentVersion   — AgentReference version (env: AzureAI__WorkflowAgentVersion)
//
// Optional: ATMET_APPSETTINGS_DIR  — folder containing appsettings.json if auto-discovery fails.
//
// Multi-turn / "resume": ProjectResponsesClient maps user text + previous_response_id to CreateResponseOptions
// (see Azure SDK ProjectResponsesClient.CreateResponseAsync overloads).

using ATMET.AI.WorkflowSpike;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using OpenAI.Responses;

#pragma warning disable OPENAI001

var config = SpikeConfiguration.Build();
var endpoint = SpikeConfiguration.GetProjectEndpoint(config);
var agentName = SpikeConfiguration.GetWorkflowAgentName(config);
var agentVersion = SpikeConfiguration.GetWorkflowAgentVersion(config);
var firstMessage = args.Length > 0 ? string.Join(" ", args) : "Hello — turn one.";
const string followUpMessage = "Turn two: please acknowledge you are continuing the same conversation thread.";

if (SpikeConfiguration.LastResolvedAppSettingsDirectory != null)
    Console.WriteLine($"appsettings: {SpikeConfiguration.LastResolvedAppSettingsDirectory}");
else
    Console.WriteLine(
        "appsettings: (not found — env only). Set ATMET_APPSETTINGS_DIR to .../src/ATMET.AI.Api or run from repo/service root.");

Console.WriteLine($"Endpoint: {endpoint}");
Console.WriteLine($"Agent: {agentName} v{agentVersion}");
Console.WriteLine($"Turn 1 message: {firstMessage}");
Console.WriteLine();

var credential = new DefaultAzureCredential();
var projectClient = new AIProjectClient(new Uri(endpoint), credential);
var openAi = projectClient.ProjectOpenAIClient;

Console.WriteLine("Creating project conversation...");
var conversation = (await openAi.GetProjectConversationsClient().CreateProjectConversationAsync()).Value;
Console.WriteLine($"Conversation id: {conversation.Id}");

var agentReference = new AgentReference(agentName, agentVersion);
Console.WriteLine("Binding ProjectResponsesClient to agent + conversation...");
var responseClient = openAi.GetProjectResponsesClientForAgent(agentReference, conversation);

Console.WriteLine();
Console.WriteLine("=== Turn 1: CreateResponseAsync (no previous_response_id) ===");
var response1 = (await responseClient.CreateResponseAsync(firstMessage)).Value;
Console.WriteLine($"response.id: {response1.Id}");
Console.WriteLine("--- Output ---");
Console.WriteLine(response1.GetOutputText());

Console.WriteLine();
Console.WriteLine("=== Turn 2: resume via overload (previousResponseId = turn 1 id) ===");
var response2 = (await responseClient.CreateResponseAsync(followUpMessage, previousResponseId: response1.Id)).Value;
Console.WriteLine($"response.id: {response2.Id}");
Console.WriteLine("--- Output ---");
Console.WriteLine(response2.GetOutputText());

Console.WriteLine();
Console.WriteLine("=== Turn 3: same chain via CreateResponseOptions.PreviousResponseId (explicit) ===");
var opts = new CreateResponseOptions
{
    PreviousResponseId = response2.Id,
    InputItems = { ResponseItem.CreateUserMessageItem("Turn three: one short sentence confirming options-based resume.") }
};
var response3 = (await responseClient.CreateResponseAsync(opts)).Value;
Console.WriteLine($"response.id: {response3.Id}");
Console.WriteLine("--- Output ---");
Console.WriteLine(response3.GetOutputText());

Console.WriteLine();
Console.WriteLine("--- Spike complete (conversation + 3 linked responses) ---");
