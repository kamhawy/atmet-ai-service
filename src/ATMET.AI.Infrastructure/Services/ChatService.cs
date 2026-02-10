using System.ClientModel;
using System.Runtime.CompilerServices;
using Azure.AI.OpenAI;
using Azure.AI.Projects;
using Azure.Identity;
using ATMET.AI.Core.Models.Requests;
using ATMET.AI.Core.Models.Responses;
using ATMET.AI.Core.Services;
using ATMET.AI.Infrastructure.Clients;
using ATMET.AI.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace ATMET.AI.Infrastructure.Services;

/// <summary>
/// Service for chat completions using Azure OpenAI via the AI Foundry project connection.
/// Obtains an authenticated AzureOpenAIClient from the project's OpenAI connection,
/// then delegates to the OpenAI ChatClient for completions and streaming.
/// </summary>
public class ChatService : IChatService
{
    private readonly ILogger<ChatService> _logger;
    private readonly AIProjectClient _projectClient;
    private readonly AzureAIOptions _options;
    private readonly Lazy<AzureOpenAIClient> _openAIClient;

    public ChatService(
        AzureAIClientFactory clientFactory,
        IOptions<AzureAIOptions> options,
        ILogger<ChatService> logger)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _projectClient = clientFactory.GetProjectClient();
        _openAIClient = new Lazy<AzureOpenAIClient>(CreateOpenAIClient);
    }

    public async Task<ChatCompletionResponse> CreateCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var deploymentName = request.Model ?? _options.DefaultModelDeployment;

            _logger.LogInformation(
                "Creating chat completion with model: {Model}, messages: {Count}",
                deploymentName, request.Messages.Count);

            var chatClient = _openAIClient.Value.GetChatClient(deploymentName);
            var chatMessages = MapToChatMessages(request.Messages);
            var chatOptions = BuildChatOptions(request);

            var result = await chatClient.CompleteChatAsync(
                chatMessages,
                chatOptions,
                cancellationToken);

            ChatCompletion completion = result.Value;

            _logger.LogInformation(
                "Chat completion successful. Tokens: prompt={Prompt}, completion={Completion}",
                completion.Usage.InputTokenCount,
                completion.Usage.OutputTokenCount);

            return new ChatCompletionResponse(
                Id: completion.Id ?? Guid.NewGuid().ToString(),
                Choices: completion.Content.Select((content, idx) => new ChatChoice(
                    Index: idx,
                    Message: new Core.Models.Requests.ChatMessage(
                        Role: "assistant",
                        Content: content.Text ?? string.Empty),
                    FinishReason: completion.FinishReason.ToString()
                )).ToList(),
                Usage: new Usage(
                    PromptTokens: completion.Usage.InputTokenCount,
                    CompletionTokens: completion.Usage.OutputTokenCount,
                    TotalTokens: completion.Usage.TotalTokenCount),
                Created: completion.CreatedAt
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create chat completion");
            throw;
        }
    }

    public async IAsyncEnumerable<ChatCompletionChunk> CreateStreamingCompletionAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var deploymentName = request.Model ?? _options.DefaultModelDeployment;

        _logger.LogInformation(
            "Creating streaming chat completion with model: {Model}, messages: {Count}",
            deploymentName, request.Messages.Count);

        var chatClient = _openAIClient.Value.GetChatClient(deploymentName);
        var chatMessages = MapToChatMessages(request.Messages);
        var chatOptions = BuildChatOptions(request);

        AsyncCollectionResult<StreamingChatCompletionUpdate> stream;

        try
        {
            stream = chatClient.CompleteChatStreamingAsync(
                chatMessages,
                chatOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate streaming chat completion");
            throw;
        }

        await foreach (var update in stream.WithCancellation(cancellationToken))
        {
            // Each update may contain one or more content parts (Azure.AI.OpenAI: Id renamed to CompletionId)
            var deltaContent = update.ContentUpdate
                .Select(c => c.Text)
                .Where(t => t != null)
                .Aggregate(string.Empty, (acc, t) => acc + t);

            yield return new ChatCompletionChunk(
                Id: update.CompletionId ?? string.Empty,
                Choices: new List<ChatChoiceDelta>
                {
                    new(
                        Index: 0,
                        Content: string.IsNullOrEmpty(deltaContent) ? null : deltaContent,
                        FinishReason: update.FinishReason?.ToString()
                    )
                },
                Created: update.CreatedAt
            );
        }

        _logger.LogInformation("Streaming chat completion finished for model: {Model}", deploymentName);
    }

    // ====================================================================
    // Private Helpers
    // ====================================================================

    /// <summary>
    /// Creates an AzureOpenAIClient by obtaining the OpenAI connection from the project.
    /// </summary>
    private AzureOpenAIClient CreateOpenAIClient()
    {
        try
        {
            _logger.LogInformation("Creating AzureOpenAIClient from project connection");

            var connection = _projectClient.GetConnection(typeof(AzureOpenAIClient).FullName!);

            if (!connection.TryGetLocatorAsUri(out Uri? uri) || uri is null)
            {
                throw new InvalidOperationException(
                    "Could not resolve Azure OpenAI endpoint from project connection. " +
                    "Ensure an Azure OpenAI resource is connected to your AI Foundry project.");
            }

            // The connection URI may include path segments; we only need the host
            var endpoint = new Uri($"https://{uri.Host}");

            var credential = !string.IsNullOrEmpty(_options.ManagedIdentityClientId)
                ? (Azure.Core.TokenCredential)new ManagedIdentityCredential(_options.ManagedIdentityClientId)
                : new DefaultAzureCredential();

            _logger.LogInformation(
                "AzureOpenAIClient created for endpoint: {Endpoint}", endpoint);

            return new AzureOpenAIClient(endpoint, credential);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create AzureOpenAIClient");
            throw;
        }
    }

    private static List<OpenAI.Chat.ChatMessage> MapToChatMessages(
        List<Core.Models.Requests.ChatMessage> messages)
    {
        return messages.Select<Core.Models.Requests.ChatMessage, OpenAI.Chat.ChatMessage>(m =>
            m.Role.ToLowerInvariant() switch
            {
                "system" => new SystemChatMessage(m.Content),
                "assistant" => new AssistantChatMessage(m.Content),
                "user" => new UserChatMessage(m.Content),
                _ => new UserChatMessage(m.Content)
            }).ToList();
    }

    private static ChatCompletionOptions BuildChatOptions(ChatCompletionRequest request)
    {
        var options = new ChatCompletionOptions();

        if (request.Temperature.HasValue)
            options.Temperature = (float)request.Temperature.Value;

        if (request.MaxTokens.HasValue)
            options.MaxOutputTokenCount = request.MaxTokens.Value;

        return options;
    }
}
