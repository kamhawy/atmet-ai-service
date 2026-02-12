using ATMET.AI.Core.Exceptions;
using ATMET.AI.Core.Models.Responses;
using ATMET.AI.Core.Services;
using ATMET.AI.Infrastructure.Clients;
using Azure.AI.Projects;
using Microsoft.Extensions.Logging;

namespace ATMET.AI.Infrastructure.Services;

/// <summary>
/// Service for managing Azure AI Search indexes via the AI Foundry SDK.
/// </summary>
public class IndexService : IIndexService
{
    private readonly ILogger<IndexService> _logger;
    private readonly AIProjectClient _projectClient;

    public IndexService(
        AzureAIClientFactory clientFactory,
        ILogger<IndexService> logger)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _projectClient = clientFactory.GetProjectClient();
    }

    public Task<IndexResponse> CreateOrUpdateIndexAsync(
        string name,
        string version,
        string connectionName,
        string indexName,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Creating/updating index: {Name} v{Version}, AI Search index: {IndexName}",
                name, version, indexName);

            var searchIndex = new AzureAISearchIndex(connectionName, indexName)
            {
                Description = description
            };

            var result = (AzureAISearchIndex)_projectClient.Indexes.CreateOrUpdate(
                name: name,
                version: version,
                index: searchIndex);

            _logger.LogInformation("Successfully created/updated index: {IndexId}", result.Id);

            return Task.FromResult(MapToIndexResponse(result, connectionName, indexName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create/update index: {Name} v{Version}", name, version);
            throw;
        }
    }

    public Task<List<IndexResponse>> ListIndexesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Listing all indexes");

            var indexes = new List<IndexResponse>();

            foreach (var index in _projectClient.Indexes.GetIndexes())
            {
                indexes.Add(MapToIndexResponse(index));
            }

            _logger.LogInformation("Retrieved {Count} indexes", indexes.Count);
            return Task.FromResult(indexes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list indexes");
            throw;
        }
    }

    public Task<List<IndexResponse>> ListIndexVersionsAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Listing versions for index: {Name}", name);

            var versions = new List<IndexResponse>();

            foreach (var index in _projectClient.Indexes.GetIndexVersions(name))
            {
                versions.Add(MapToIndexResponse(index));
            }

            _logger.LogInformation("Retrieved {Count} versions for index: {Name}",
                versions.Count, name);

            return Task.FromResult(versions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list index versions: {Name}", name);
            throw;
        }
    }

    public Task<IndexResponse> GetIndexAsync(
        string name,
        string version,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting index: {Name} v{Version}", name, version);

            var index = _projectClient.Indexes.GetIndex(name, version);

            return Task.FromResult(MapToIndexResponse(index));
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            throw new NotFoundException($"Index '{name}' version '{version}' not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get index: {Name} v{Version}", name, version);
            throw;
        }
    }

    public Task DeleteIndexAsync(
        string name,
        string version,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Deleting index: {Name} v{Version}", name, version);

            _projectClient.Indexes.Delete(name, version);

            _logger.LogInformation("Successfully deleted index: {Name} v{Version}", name, version);
            return Task.CompletedTask;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            throw new NotFoundException($"Index '{name}' version '{version}' not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete index: {Name} v{Version}", name, version);
            throw;
        }
    }

    // ====================================================================
    // Private Helpers
    // ====================================================================

    private static IndexResponse MapToIndexResponse(
        AIProjectIndex index,
        string? connectionName = null,
        string? indexName = null)
    {
        // Extract connection/index name from AzureAISearchIndex if available
        string resolvedConnectionName = connectionName ?? string.Empty;
        string resolvedIndexName = indexName ?? string.Empty;
        string? description = index.Description;
        object? fieldMapping = null;

        if (index is AzureAISearchIndex searchIndex)
        {
            resolvedConnectionName = searchIndex.ConnectionName ?? resolvedConnectionName;
            resolvedIndexName = searchIndex.IndexName ?? resolvedIndexName;
            description = searchIndex.Description ?? description;
            fieldMapping = searchIndex.FieldMapping;
        }

        var tags = index.Tags?.Keys.ToDictionary(k => k, k => index.Tags[k]?.ToString() ?? string.Empty);

        return new IndexResponse(
            Id: index.Id,
            Name: index.Name,
            Version: index.Version,
            ConnectionName: resolvedConnectionName,
            IndexName: resolvedIndexName,
            Description: description,
            CreatedAt: DateTimeOffset.UtcNow, // SDK may not expose CreatedAt directly
            IndexType: index is AzureAISearchIndex ? "AzureSearch" : index.GetType().Name,
            Tags: tags,
            FieldMapping: fieldMapping
        );
    }
}
