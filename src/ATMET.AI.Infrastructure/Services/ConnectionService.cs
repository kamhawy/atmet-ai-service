using ATMET.AI.Core.Exceptions;
using ATMET.AI.Core.Models.Responses;
using ATMET.AI.Core.Services;
using ATMET.AI.Infrastructure.Clients;
using Azure.AI.Projects;
using Microsoft.Extensions.Logging;

namespace ATMET.AI.Infrastructure.Services;

public class ConnectionService : IConnectionService
{
    private readonly AzureAIClientFactory _clientFactory;
    private readonly ILogger<ConnectionService> _logger;
    private readonly AIProjectClient _projectClient;

    public ConnectionService(
        AzureAIClientFactory clientFactory,
        ILogger<ConnectionService> logger)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _projectClient = _clientFactory.GetProjectClient();
    }

    public async Task<List<ConnectionResponse>> ListConnectionsAsync(
        string? connectionType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Listing connections with type: {Type}", connectionType);

            var connections = new List<ConnectionResponse>();
            ConnectionType? type = connectionType != null
                ? Enum.Parse<ConnectionType>(connectionType)
                : null;

            var connectionPages = _projectClient.Connections.GetConnectionsAsync(
                connectionType: type,
                cancellationToken: cancellationToken);

            await foreach (var connection in connectionPages)
            {
                connections.Add(MapToConnectionResponse(connection));
            }

            _logger.LogInformation("Retrieved {Count} connections", connections.Count);
            return connections;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list connections");
            throw;
        }
    }

    public async Task<ConnectionResponse> GetConnectionAsync(
        string connectionName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting connection: {ConnectionName}", connectionName);

            var connection = await _projectClient.Connections.GetConnectionAsync(
                connectionName: connectionName,
                includeCredentials: false,
                cancellationToken: cancellationToken);

            if (connection?.Value == null)
                throw new NotFoundException($"Connection '{connectionName}' not found");

            return MapToConnectionResponse(connection.Value);
        }
        catch (NotFoundException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get connection: {ConnectionName}", connectionName);
            throw;
        }
    }

    public async Task<ConnectionResponse> GetDefaultConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting default connection");

            var connection = await _projectClient.Connections.GetDefaultConnectionAsync(
                includeCredentials: false);

            if (connection == null)
                throw new NotFoundException("Default connection not found");

            return MapToConnectionResponse(connection);
        }
        catch (NotFoundException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get default connection");
            throw;
        }
    }

    private static ConnectionResponse MapToConnectionResponse(AIProjectConnection connection)
    {
        return new ConnectionResponse(
            Name: connection.Name,
            Type: connection.Type.ToString(),
            Target: connection.Target,
            Properties: connection.Metadata?.ToDictionary(
                k => k.Key,
                v => (object)v.Value) ?? new Dictionary<string, object>(),
            Id: connection.Id,
            IsDefault: connection.IsDefault
        );
    }
}
