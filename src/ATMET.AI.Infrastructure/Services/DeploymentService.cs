using ATMET.AI.Core.Exceptions;
using ATMET.AI.Core.Models.Responses;
using ATMET.AI.Core.Services;
using ATMET.AI.Infrastructure.Clients;
using Azure.AI.Projects;
using Microsoft.Extensions.Logging;

namespace ATMET.AI.Infrastructure.Services;

/// <summary>
/// Service for managing AI model deployments
/// </summary>
public class DeploymentService : IDeploymentService
{
    private readonly ILogger<DeploymentService> _logger;
    private readonly AIProjectClient _projectClient;

    public DeploymentService(
        AzureAIClientFactory clientFactory,
        ILogger<DeploymentService> logger)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _projectClient = clientFactory.GetProjectClient();
    }

    public async Task<List<DeploymentResponse>> ListDeploymentsAsync(
        string? modelPublisher = null,
        string? modelType = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Listing deployments with publisher: {Publisher}, type: {Type}",
                modelPublisher, modelType);

            var deployments = new List<DeploymentResponse>();

            var deploymentPages = _projectClient.Deployments.GetDeploymentsAsync(
                modelPublisher: modelPublisher,
                cancellationToken: cancellationToken);

            await foreach (var deployment in deploymentPages)
            {
                deployments.Add(MapToDeploymentResponse(deployment));
            }

            _logger.LogInformation("Retrieved {Count} deployments", deployments.Count);
            return deployments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list deployments");
            throw;
        }
    }

    public async Task<DeploymentResponse> GetDeploymentAsync(
        string deploymentName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting deployment: {DeploymentName}", deploymentName);

            var deployment = await _projectClient.Deployments.GetDeploymentAsync(
                deploymentName,
                cancellationToken);

            if (deployment?.Value == null)
                throw new NotFoundException($"Deployment '{deploymentName}' not found");

            return MapToDeploymentResponse(deployment.Value);
        }
        catch (NotFoundException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get deployment: {DeploymentName}", deploymentName);
            throw;
        }
    }

    private static DeploymentResponse MapToDeploymentResponse(AIProjectDeployment deployment)
    {
        var modelDeployment = deployment as ModelDeployment;
        var capabilities = modelDeployment?.Capabilities?.ToDictionary(
            k => k.Key,
            v => (object)v.Value);

        return new DeploymentResponse(
            Name: deployment.Name ?? "Unknown",
            Model: modelDeployment?.ModelName ?? deployment.Name ?? "Unknown",
            Publisher: modelDeployment?.ModelPublisher ?? "Unknown",
            Type: modelDeployment != null ? "ModelDeployment" : deployment.GetType().Name,
            Status: "Active",
            ModelVersion: modelDeployment?.ModelVersion,
            ConnectionName: modelDeployment?.ConnectionName,
            Sku: modelDeployment?.Sku?.Name,
            Capabilities: capabilities
        );
    }
}
