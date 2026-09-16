namespace DocConversionService.Domain.Parsing;

public record ValidationResult(bool IsValid, string? FailureReason = null);
