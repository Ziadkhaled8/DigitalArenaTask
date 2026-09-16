namespace DocConversionService.Infrastructure.Tests;

using DocConversionService.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using Xunit;

public class LocalDiskFileStorageProviderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LocalDiskFileStorageProvider _storage;

    public LocalDiskFileStorageProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DocConversionTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var options = Options.Create(new StorageSettings { RootPath = _tempDir });
        _storage = new LocalDiskFileStorageProvider(options);
    }

    [Fact]
    public async Task SaveAndGetAsync_RoundTripsContentCorrectly()
    {
        // Arrange
        var key = "jobs/123/part-1.html";
        var content = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"

        // Act
        var savedKey = await _storage.SaveAsync(key, content);
        var retrieved = await _storage.GetAsync(key);

        // Assert
        Assert.Equal(key, savedKey);
        Assert.Equal(content, retrieved);
    }

    [Fact]
    public async Task GetAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => _storage.GetAsync("non-existent/file.bin"));
    }

    [Fact]
    public async Task DeleteAsync_ExistingFile_RemovesFile()
    {
        // Arrange
        var key = "to-delete/sample.txt";
        await _storage.SaveAsync(key, new byte[] { 1, 2, 3 });

        // Act
        await _storage.DeleteAsync(key);

        // Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => _storage.GetAsync(key));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
