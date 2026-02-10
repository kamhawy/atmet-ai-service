using ATMET.AI.Core.Models.Requests;
using ATMET.AI.Core.Models.Responses;
using ATMET.AI.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ATMET.AI.Api.Endpoints;

/// <summary>
/// Endpoints for managing Azure AI Agents
/// </summary>
public static class AgentsEndpoints
{
    public static void MapEndpoints(RouteGroupBuilder group)
    {
        var agents = group.MapGroup("/agents")
            .WithTags("Agents")
            .WithOpenApi();

        // ====================================================================
        // Agent Management
        // ====================================================================

        agents.MapPost("/", CreateAgent)
            .WithName("CreateAgent")
            .WithSummary("Create a new AI agent")
            .RequireAuthorization("ApiWriter")
            .Produces<AgentResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        agents.MapGet("/", ListAgents)
            .WithName("ListAgents")
            .WithSummary("List all agents")
            .Produces<List<AgentResponse>>()
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        agents.MapGet("/{agentId}", GetAgent)
            .WithName("GetAgent")
            .WithSummary("Get agent by ID")
            .Produces<AgentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        agents.MapPut("/{agentId}", UpdateAgent)
            .WithName("UpdateAgent")
            .WithSummary("Update an existing agent")
            .RequireAuthorization("ApiWriter")
            .Produces<AgentResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        agents.MapDelete("/{agentId}", DeleteAgent)
            .WithName("DeleteAgent")
            .WithSummary("Delete an agent")
            .RequireAuthorization("ApiWriter")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // ====================================================================
        // Thread Management
        // ====================================================================

        agents.MapPost("/{agentId}/threads", CreateThread)
            .WithName("CreateThread")
            .WithSummary("Create a new conversation thread for an agent")
            .Produces<ThreadResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound);

        agents.MapGet("/threads/{threadId}", GetThread)
            .WithName("GetThread")
            .WithSummary("Get thread by ID")
            .Produces<ThreadResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        agents.MapDelete("/threads/{threadId}", DeleteThread)
            .WithName("DeleteThread")
            .WithSummary("Delete a thread")
            .RequireAuthorization("ApiWriter")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // ====================================================================
        // Message Management
        // ====================================================================

        agents.MapPost("/threads/{threadId}/messages", AddMessage)
            .WithName("AddMessage")
            .WithSummary("Add a message to a thread")
            .Produces<MessageResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        agents.MapGet("/threads/{threadId}/messages", GetMessages)
            .WithName("GetMessages")
            .WithSummary("Get all messages in a thread")
            .Produces<List<MessageResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        agents.MapGet("/threads/{threadId}/messages/{messageId}", GetMessage)
            .WithName("GetMessage")
            .WithSummary("Get a specific message")
            .Produces<MessageResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // ====================================================================
        // Run Management
        // ====================================================================

        agents.MapPost("/threads/{threadId}/runs", CreateRun)
            .WithName("CreateRun")
            .WithSummary("Create and execute a run")
            .Produces<RunResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        agents.MapGet("/threads/{threadId}/runs/{runId}", GetRun)
            .WithName("GetRun")
            .WithSummary("Get run status and details")
            .Produces<RunResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        agents.MapPost("/threads/{threadId}/runs/{runId}/cancel", CancelRun)
            .WithName("CancelRun")
            .WithSummary("Cancel a running execution")
            .Produces<RunResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        agents.MapGet("/threads/{threadId}/runs", ListRuns)
            .WithName("ListRuns")
            .WithSummary("List all runs for a thread")
            .Produces<List<RunResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // ====================================================================
        // File Management
        // ====================================================================

        agents.MapPost("/files", UploadFile)
            .WithName("UploadAgentFile")
            .WithSummary("Upload a file for agent use")
            .DisableAntiforgery()
            .Produces<FileResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        agents.MapGet("/files/{fileId}", GetFile)
            .WithName("GetAgentFile")
            .WithSummary("Get file metadata")
            .Produces<FileResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        agents.MapDelete("/files/{fileId}", DeleteFile)
            .WithName("DeleteAgentFile")
            .WithSummary("Delete a file")
            .RequireAuthorization("ApiWriter")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    // ====================================================================
    // Handler Methods
    // ====================================================================

    private static async Task<IResult> CreateAgent(
        [FromBody] CreateAgentRequest request,
        [FromServices] IAgentService agentService,
        CancellationToken cancellationToken)
    {
        var agent = await agentService.CreateAgentAsync(request, cancellationToken);
        return Results.Created($"/api/v1/agents/{agent.Id}", agent);
    }

    private static async Task<IResult> ListAgents(
        [FromServices] IAgentService agentService,
        [FromQuery] int? limit,
        [FromQuery] string? order,
        CancellationToken cancellationToken)
    {
        var agents = await agentService.ListAgentsAsync(limit, order, cancellationToken);
        return Results.Ok(agents);
    }

    private static async Task<IResult> GetAgent(
        string agentId,
        [FromServices] IAgentService agentService,
        CancellationToken cancellationToken)
    {
        var agent = await agentService.GetAgentAsync(agentId, cancellationToken);
        return Results.Ok(agent);
    }

    private static async Task<IResult> UpdateAgent(
        string agentId,
        [FromBody] UpdateAgentRequest request,
        [FromServices] IAgentService agentService,
        CancellationToken cancellationToken)
    {
        var agent = await agentService.UpdateAgentAsync(agentId, request, cancellationToken);
        return Results.Ok(agent);
    }

    private static async Task<IResult> DeleteAgent(
        string agentId,
        [FromServices] IAgentService agentService,
        CancellationToken cancellationToken)
    {
        await agentService.DeleteAgentAsync(agentId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> CreateThread(
        string agentId,
        [FromBody] CreateThreadRequest? request,
        [FromServices] IAgentService agentService,
        CancellationToken cancellationToken)
    {
        var thread = await agentService.CreateThreadAsync(agentId, request, cancellationToken);
        return Results.Created($"/api/v1/agents/threads/{thread.Id}", thread);
    }

    private static async Task<IResult> GetThread(
        string threadId,
        [FromServices] IAgentService agentService,
        CancellationToken cancellationToken)
    {
        var thread = await agentService.GetThreadAsync(threadId, cancellationToken);
        return Results.Ok(thread);
    }

    private static async Task<IResult> DeleteThread(
        string threadId,
        [FromServices] IAgentService agentService,
        CancellationToken cancellationToken)
    {
        await agentService.DeleteThreadAsync(threadId, cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> AddMessage(
        string threadId,
        [FromBody] CreateMessageRequest request,
        [FromServices] IAgentService agentService,
        CancellationToken cancellationToken)
    {
        var message = await agentService.AddMessageAsync(threadId, request, cancellationToken);
        return Results.Created($"/api/v1/agents/threads/{threadId}/messages/{message.Id}", message);
    }

    private static async Task<IResult> GetMessages(
        string threadId,
        [FromServices] IAgentService agentService,
        [FromQuery] int? limit,
        [FromQuery] string? order,
        CancellationToken cancellationToken)
    {
        var messages = await agentService.GetMessagesAsync(threadId, limit, order, cancellationToken);
        return Results.Ok(messages);
    }

    private static async Task<IResult> GetMessage(
        string threadId,
        string messageId,
        [FromServices] IAgentService agentService,
        CancellationToken cancellationToken)
    {
        var message = await agentService.GetMessageAsync(threadId, messageId, cancellationToken);
        return Results.Ok(message);
    }

    private static async Task<IResult> CreateRun(
        string threadId,
        [FromBody] CreateRunRequest request,
        [FromServices] IAgentService agentService,
        CancellationToken cancellationToken)
    {
        var run = await agentService.CreateRunAsync(threadId, request, cancellationToken);
        return Results.Created($"/api/v1/agents/threads/{threadId}/runs/{run.Id}", run);
    }

    private static async Task<IResult> GetRun(
        string threadId,
        string runId,
        [FromServices] IAgentService agentService,
        CancellationToken cancellationToken)
    {
        var run = await agentService.GetRunAsync(threadId, runId, cancellationToken);
        return Results.Ok(run);
    }

    private static async Task<IResult> CancelRun(
        string threadId,
        string runId,
        [FromServices] IAgentService agentService,
        CancellationToken cancellationToken)
    {
        var run = await agentService.CancelRunAsync(threadId, runId, cancellationToken);
        return Results.Ok(run);
    }

    private static async Task<IResult> ListRuns(
        string threadId,
        [FromServices] IAgentService agentService,
        [FromQuery] int? limit,
        [FromQuery] string? order,
        CancellationToken cancellationToken)
    {
        var runs = await agentService.ListRunsAsync(threadId, limit, order, cancellationToken);
        return Results.Ok(runs);
    }

    private static async Task<IResult> UploadFile(
        IFormFile file,
        [FromServices] IAgentService agentService,
        CancellationToken cancellationToken)
    {
        // Unwrap IFormFile → Stream at the API boundary
        await using var stream = file.OpenReadStream();
        var fileResponse = await agentService.UploadFileAsync(stream, file.FileName, cancellationToken);
        return Results.Created($"/api/v1/agents/files/{fileResponse.Id}", fileResponse);
    }

    private static async Task<IResult> GetFile(
        string fileId,
        [FromServices] IAgentService agentService,
        CancellationToken cancellationToken)
    {
        var fileResp = await agentService.GetFileAsync(fileId, cancellationToken);
        return Results.Ok(fileResp);
    }

    private static async Task<IResult> DeleteFile(
        string fileId,
        [FromServices] IAgentService agentService,
        CancellationToken cancellationToken)
    {
        await agentService.DeleteFileAsync(fileId, cancellationToken);
        return Results.NoContent();
    }
}
