using Microsoft.Extensions.Configuration;
using SecurityRecap.Core.Interfaces;

namespace SecurityRecap.Infrastructure.Services;

public class LocalFileStorageService : IBlobStorageService
{
    private readonly string _rootPath;

    public LocalFileStorageService(IConfiguration config)
    {
        _rootPath = config["LocalStorage:RootPath"]
            ?? Path.Combine(AppContext.BaseDirectory, "storage");
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> UploadAsync(Stream content, string fileName, string contentType)
    {
        var relative = Path.Combine(
            DateTime.UtcNow.ToString("yyyy"),
            DateTime.UtcNow.ToString("MM"),
            DateTime.UtcNow.ToString("dd"),
            Guid.NewGuid().ToString(),
            fileName);

        var absolute = Path.Combine(_rootPath, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);

        await using (var fs = File.Create(absolute))
        {
            await content.CopyToAsync(fs);
        }

        return new Uri(absolute).AbsoluteUri;
    }

    public Task<Stream> DownloadAsync(string blobUrl)
    {
        var path = new Uri(blobUrl).LocalPath;
        Stream stream = File.OpenRead(path);
        return Task.FromResult(stream);
    }
}
