namespace DocConversionService.Domain.Interfaces;

public interface IFileStorageProvider
{
    Task<string> SaveAsync(string key, byte[] content);
    Task<byte[]> GetAsync(string key);
    Task DeleteAsync(string key);
}
