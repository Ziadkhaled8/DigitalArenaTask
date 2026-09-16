namespace DocConversionService.Domain.Exceptions;

using DocConversionService.Domain.Enums;

public class UnsupportedFormatException : DocumentProcessingException
{
    public UnsupportedFormatException(string message)
        : base(ErrorCode.UnsupportedFormat, message) { }
}
