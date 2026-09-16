namespace DocConversionService.Infrastructure.Storage;

using DocConversionService.Domain.Interfaces;
using Microsoft.Extensions.Options;

public class LocalDiskFileStorageProvider : IFileStorageProvider
{
    private readonly string _rootPath;

    public LocalDiskFileStorageProvider(IOptions<StorageSettings> settings)
    {
        _rootPath = Path.GetFullPath(settings.Value.RootPath);
    }

    public async Task<string> SaveAsync(string key, byte[] content)
    {
        var filePath = GetFullPath(key);
        var directory = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(filePath, content);
        return key;
    }

    public async Task<byte[]> GetAsync(string key)
    {
        var filePath = GetFullPath(key);
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {key}", key);
        return await File.ReadAllBytesAsync(filePath);
    }

    public Task DeleteAsync(string key)
    {
        var filePath = GetFullPath(key);
        if (File.Exists(filePath))
            File.Delete(filePath);
        return Task.CompletedTask;
    }

    private string GetFullPath(string key) => Path.Combine(_rootPath, key.Replace('/', Path.DirectorySeparatorChar));
}
