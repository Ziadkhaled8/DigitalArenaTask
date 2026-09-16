namespace DocConversionService.Domain.Exceptions;

using DocConversionService.Domain.Enums;

public class DocumentProcessingException : Exception
{
    public ErrorCode ErrorCode { get; }

    public DocumentProcessingException(ErrorCode errorCode, string message)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public DocumentProcessingException(ErrorCode errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
