using ATMET.AI.Core.Exceptions;
using ATMET.AI.Core.Models.Responses;
using ATMET.AI.Core.Services;
using ATMET.AI.Infrastructure.Clients;
using Azure.AI.Projects;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace ATMET.AI.Infrastructure.Services;

/// <summary>
/// Service for managing datasets via Azure AI Foundry SDK.
/// The SDK's UploadFile/UploadFolder methods require file system paths, so
/// stream-based uploads are buffered to temp files before forwarding to the SDK.
/// </summary>
public class DatasetService : IDatasetService
{
    private readonly ILogger<DatasetService> _logger;
    private readonly AIProjectClient _projectClient;

    public DatasetService(
        AzureAIClientFactory clientFactory,
        ILogger<DatasetService> logger)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _projectClient = clientFactory.GetProjectClient();
    }

    public async Task<DatasetResponse> UploadFileAsync(
        string name,
        string version,
        Stream fileStream,
        string fileName,
        string connectionName,
        CancellationToken cancellationToken = default)
    {
        // The SDK's UploadFile requires a file path, so buffer the stream to a temp file
        var tempPath = Path.Combine(Path.GetTempPath(), $"atmet-ds-{Guid.NewGuid()}{Path.GetExtension(fileName)}");

        try
        {
            _logger.LogInformation(
                "Uploading file dataset: {Name} v{Version}, file: {FileName}",
                name, version, fileName);

            await using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
            {
                await fileStream.CopyToAsync(fs, cancellationToken);
            }

            var fileDataset = _projectClient.Datasets.UploadFile(
                name: name,
                version: version,
                filePath: tempPath,
                connectionName: connectionName);

            _logger.LogInformation(
                "Successfully created file dataset: {DatasetId}", fileDataset.Value.Id);

            return MapToDatasetResponse(fileDataset, "File");
        }
        catch (Exception ex) when (ex is not NotFoundException)
        {
            _logger.LogError(ex, "Failed to upload file dataset: {Name} v{Version}", name, version);
            throw;
        }
        finally
        {
            CleanupTempFile(tempPath);
        }
    }

    public async Task<DatasetResponse> UploadFolderAsync(
        string name,
        string version,
        IEnumerable<(Stream Stream, string FileName)> files,
        string connectionName,
        string? filePattern = null,
        CancellationToken cancellationToken = default)
    {
        // Buffer all files into a temp folder, then call SDK's UploadFolder
        var tempFolder = Path.Combine(Path.GetTempPath(), $"atmet-ds-folder-{Guid.NewGuid()}");

        try
        {
            _logger.LogInformation(
                "Uploading folder dataset: {Name} v{Version}", name, version);

            Directory.CreateDirectory(tempFolder);

            foreach (var (stream, fileName) in files)
            {
                var filePath = Path.Combine(tempFolder, fileName);
                var dir = Path.GetDirectoryName(filePath);
                if (dir != null)
                    Directory.CreateDirectory(dir);

                await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                await stream.CopyToAsync(fs, cancellationToken);
            }

            Regex? pattern = filePattern != null ? new Regex(filePattern) : null;

            var folderDataset = _projectClient.Datasets.UploadFolder(
                name: name,
                version: version,
                folderPath: tempFolder,
                connectionName: connectionName,
                filePattern: pattern);

            _logger.LogInformation(
                "Successfully created folder dataset: {DatasetId}", folderDataset.Value.Id);

            return MapToDatasetResponse(folderDataset, "Folder");
        }
        catch (Exception ex) when (ex is not NotFoundException)
        {
            _logger.LogError(ex, "Failed to upload folder dataset: {Name} v{Version}", name, version);
            throw;
        }
        finally
        {
            CleanupTempFolder(tempFolder);
        }
    }

    public Task<List<DatasetResponse>> ListDatasetsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Listing all datasets");

            var datasets = new List<DatasetResponse>();

            foreach (var dataset in _projectClient.Datasets.GetDatasets())
            {
                datasets.Add(MapToDatasetResponse(dataset));
            }

            _logger.LogInformation("Retrieved {Count} datasets", datasets.Count);
            return Task.FromResult(datasets);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list datasets");
            throw;
        }
    }

    public Task<List<DatasetResponse>> ListDatasetVersionsAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Listing versions for dataset: {Name}", name);

            var versions = new List<DatasetResponse>();

            foreach (var dataset in _projectClient.Datasets.GetDatasetVersions(name))
            {
                versions.Add(MapToDatasetResponse(dataset));
            }

            _logger.LogInformation("Retrieved {Count} versions for dataset: {Name}",
                versions.Count, name);

            return Task.FromResult(versions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list dataset versions: {Name}", name);
            throw;
        }
    }

    public Task<DatasetResponse> GetDatasetAsync(
        string name,
        string version,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting dataset: {Name} v{Version}", name, version);

            var dataset = _projectClient.Datasets.GetDataset(name, version);

            return Task.FromResult(MapToDatasetResponse(dataset));
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            throw new NotFoundException($"Dataset '{name}' version '{version}' not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get dataset: {Name} v{Version}", name, version);
            throw;
        }
    }

    public Task<DatasetCredentialsResponse> GetCredentialsAsync(
        string name,
        string version,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting credentials for dataset: {Name} v{Version}", name, version);

            var credentials = _projectClient.Datasets.GetCredentials(name, version);

            // DatasetCredential has BlobReference (AIProjectBlobReference); no ExpirationDateTime in SDK 1.1.0.
            var blobRef = credentials.Value.BlobReference;
            var sasCred = blobRef?.Credential;
            var sasUri = sasCred?.SasUri?.ToString() ?? string.Empty;
            return Task.FromResult(new DatasetCredentialsResponse(
                SasUri: sasUri,
                ExpiresAt: DateTimeOffset.UtcNow.AddHours(1),
                StorageAccountArmId: blobRef?.StorageAccountArmId,
                BlobUri: blobRef?.BlobUri?.ToString(),
                CredentialType: sasCred?.Type
            ));
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            throw new NotFoundException($"Dataset '{name}' version '{version}' not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get dataset credentials: {Name} v{Version}", name, version);
            throw;
        }
    }

    public Task DeleteDatasetAsync(
        string name,
        string version,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Deleting dataset: {Name} v{Version}", name, version);

            _projectClient.Datasets.Delete(name, version);

            _logger.LogInformation("Successfully deleted dataset: {Name} v{Version}", name, version);
            return Task.CompletedTask;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            throw new NotFoundException($"Dataset '{name}' version '{version}' not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete dataset: {Name} v{Version}", name, version);
            throw;
        }
    }

    // ====================================================================
    // Private Helpers
    // ====================================================================

    private static DatasetResponse MapToDatasetResponse(AIProjectDataset dataset, string? type = null)
    {
        var tags = dataset.Tags?.Keys.ToDictionary(k => k, k => dataset.Tags[k]?.ToString() ?? string.Empty);

        return new DatasetResponse(
            Id: dataset.Id,
            Name: dataset.Name,
            Version: dataset.Version,
            Type: type ?? dataset.GetType().Name.Replace("Dataset", ""),
            CreatedAt: DateTimeOffset.UtcNow, // SDK type may not expose CreatedAt directly
            Description: dataset.Description,
            ConnectionName: dataset.ConnectionName,
            IsReference: dataset.IsReference,
            DataUri: dataset.DataUri?.ToString(),
            Tags: tags
        );
    }

    private void CleanupTempFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up temp file: {Path}", path);
        }
    }

    private void CleanupTempFolder(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up temp folder: {Path}", path);
        }
    }
}
