using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using SecurityRecap.Core.Interfaces;

namespace SecurityRecap.Infrastructure.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _containerClient;

    public BlobStorageService(IConfiguration config)
    {
        var connectionString = config["Azure:BlobStorage:ConnectionString"]
            ?? throw new InvalidOperationException("Azure:BlobStorage:ConnectionString is not configured");
        var containerName = config["Azure:BlobStorage:ContainerName"] ?? "reports";

        var blobServiceClient = new BlobServiceClient(connectionString);
        _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
    }

    public async Task<string> UploadAsync(Stream content, string fileName, string contentType)
    {
        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

        var blobName = $"{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid()}/{fileName}";
        var blobClient = _containerClient.GetBlobClient(blobName);

        await blobClient.UploadAsync(content, new BlobHttpHeaders { ContentType = contentType });

        return blobClient.Uri.ToString();
    }

    public async Task<Stream> DownloadAsync(string blobUrl)
    {
        var uri = new Uri(blobUrl);
        var blobName = string.Join("/", uri.Segments.Skip(2)).TrimStart('/');
        var blobClient = _containerClient.GetBlobClient(blobName);

        var download = await blobClient.DownloadStreamingAsync();
        return download.Value.Content;
    }
}
