namespace DocConversionService.Application.Dtos;

public record OutputPartDto(
    int PartNumber,
    int TotalParts,
    long SizeBytes,
    bool ExceedsSizeLimit,
    string DownloadUrl
);
