using Azure.Storage.Blobs;
using BARD.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace BARD.Infrastructure.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;

    public BlobStorageService(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AzureBlobStorage")
            ?? throw new InvalidOperationException("Missing 'AzureBlobStorage' connection string.");
        _blobServiceClient = new BlobServiceClient(connectionString);
    }

    public async Task<string> UploadAsync(string containerName, string fileName, Stream content, string contentType, CancellationToken ct = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: ct);

        var blobName = $"{Guid.NewGuid()}/{fileName}";
        var blobClient = containerClient.GetBlobClient(blobName);

        await blobClient.UploadAsync(content, new Azure.Storage.Blobs.Models.BlobHttpHeaders { ContentType = contentType }, cancellationToken: ct);

        return $"{containerName}/{blobName}";
    }

    public async Task<Stream> DownloadAsync(string blobPath, CancellationToken ct = default)
    {
        var (containerName, blobName) = SplitPath(blobPath);
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        var response = await blobClient.DownloadStreamingAsync(cancellationToken: ct);
        return response.Value.Content;
    }

    public async Task DeleteAsync(string blobPath, CancellationToken ct = default)
    {
        var (containerName, blobName) = SplitPath(blobPath);
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: ct);
    }

    private static (string containerName, string blobName) SplitPath(string blobPath)
    {
        var separatorIndex = blobPath.IndexOf('/');
        if (separatorIndex < 0)
            throw new ArgumentException($"Invalid blob path: '{blobPath}'.", nameof(blobPath));
        return (blobPath[..separatorIndex], blobPath[(separatorIndex + 1)..]);
    }
}
