using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PdfStreamingWatermarker.Functions.Services;

public class AzureBlobFileService : IFileService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;
    private readonly ILogger<AzureBlobFileService> _logger;

    public AzureBlobFileService(
        BlobServiceClient blobServiceClient,
        IConfiguration configuration,
        ILogger<AzureBlobFileService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _containerName = configuration["AzureStorage:ContainerName"] ?? "pdfs";
        _logger = logger;
    }

    public async Task<Stream?> GetFileStreamAsync(string filename, CancellationToken cancellationToken)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(filename);

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                _logger.LogWarning("Blob {Filename} not found", filename);
                return null;
            }

            return await blobClient.OpenReadAsync(new Azure.Storage.Blobs.Models.BlobOpenReadOptions(false), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting blob stream for {Filename}", filename);
            return null;
        }
    }

    public async Task<bool> FileExistsAsync(string filename, CancellationToken cancellationToken)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(filename);

            return await blobClient.ExistsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking blob existence for {Filename}", filename);
            return false;
        }
    }
} 