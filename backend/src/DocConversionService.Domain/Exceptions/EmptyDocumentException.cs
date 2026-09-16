namespace DocConversionService.Domain.Exceptions;

using DocConversionService.Domain.Enums;

public class EmptyDocumentException : DocumentProcessingException
{
    public EmptyDocumentException(string message = "The document contains no extractable content.")
        : base(ErrorCode.EmptyDocument, message) { }
}
