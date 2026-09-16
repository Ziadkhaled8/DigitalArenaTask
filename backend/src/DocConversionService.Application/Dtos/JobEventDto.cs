namespace DocConversionService.Application.Dtos;

public record JobEventDto(
    string Status,
    DateTime Timestamp,
    string Message,
    string? ErrorCode
);
