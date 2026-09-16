namespace DocConversionService.Domain.Exceptions;

using DocConversionService.Domain.Enums;

public class CorruptedFileException : DocumentProcessingException
{
    public CorruptedFileException(string message)
        : base(ErrorCode.CorruptedFile, message) { }

    public CorruptedFileException(string message, Exception innerException)
        : base(ErrorCode.CorruptedFile, message, innerException) { }
}
