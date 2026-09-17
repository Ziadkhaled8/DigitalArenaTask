namespace DocConversionService.Domain.Interfaces;

public static class StorageKeys
{
    public static string Source(Guid jobId, string fileName) => $"{jobId}/source/{fileName}";

    public static string Part(Guid jobId, int partNumber, string extension) =>
        $"{jobId}/parts/part-{partNumber}.{extension}";
}