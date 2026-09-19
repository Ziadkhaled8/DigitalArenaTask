namespace DocConversionService.Infrastructure.Storage;

using DocConversionService.Application.Interfaces;
using Microsoft.Extensions.Options;

public class LocalDiskFileStorageProvider : IFileStorageProvider
{
    private readonly string _rootPath;

    public LocalDiskFileStorageProvider(IOptions<StorageSettings> settings)
    {
        _rootPath = Path.GetFullPath(settings.Value.RootPath);
    }

    public async Task<string> SaveAsync(string key, byte[] content, CancellationToken cancellationToken = default)
    {
        var filePath = GetFullPath(key);
        var directory = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(filePath, content, cancellationToken);
        return key;
    }

    public async Task<byte[]> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var filePath = GetFullPath(key);
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {key}", key);
        return await File.ReadAllBytesAsync(filePath, cancellationToken);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var filePath = GetFullPath(key);
        if (File.Exists(filePath))
            File.Delete(filePath);
        return Task.CompletedTask;
    }

    private string GetFullPath(string key) => Path.Combine(_rootPath, key.Replace('/', Path.DirectorySeparatorChar));
}
