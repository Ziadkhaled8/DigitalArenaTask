namespace DocConversionService.Domain.Enums;

public enum ErrorCode
{
    UnsupportedFormat,
    CorruptedFile,
    ScannedDocument,
    EmptyDocument,
    ValidationFailed,
    Cancelled,
    Unknown
}
