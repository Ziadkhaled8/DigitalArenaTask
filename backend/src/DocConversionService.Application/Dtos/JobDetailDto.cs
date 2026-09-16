namespace DocConversionService.Application.Dtos;

public record JobDetailDto(
    Guid Id,
    string SourceFileName,
    string RequestedFormat,
    string? ResolvedFormat,
    string Status,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    string? ErrorCode,
    string? ErrorMessage,
    IReadOnlyList<JobEventDto> Events,
    IReadOnlyList<OutputPartDto> Parts
);
