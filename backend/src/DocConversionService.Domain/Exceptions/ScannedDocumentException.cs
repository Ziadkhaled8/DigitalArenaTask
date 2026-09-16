namespace DocConversionService.Domain.Exceptions;

using DocConversionService.Domain.Enums;

public class ScannedDocumentException : DocumentProcessingException
{
    public ScannedDocumentException(string message = "The document appears to be a scanned image with no extractable text layer.")
        : base(ErrorCode.ScannedDocument, message) { }
}
