namespace DocConversionService.Application.Dtos;

public record JobSummaryDto(
    Guid Id,
    string SourceFileName,
    string RequestedFormat,
    string? ResolvedFormat,
    string Status,
    DateTime CreatedAt,
    DateTime? CompletedAt
);
