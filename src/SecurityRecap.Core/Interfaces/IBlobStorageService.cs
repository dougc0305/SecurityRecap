namespace SecurityRecap.Core.Interfaces;

public interface IBlobStorageService
{
    Task<string> UploadAsync(Stream content, string fileName, string contentType);
    Task<Stream> DownloadAsync(string blobUrl);
}
