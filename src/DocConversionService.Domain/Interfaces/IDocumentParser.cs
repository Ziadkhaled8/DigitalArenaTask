namespace DocConversionService.Domain.Interfaces;

using DocConversionService.Domain.Parsing;

public interface IDocumentParser
{
    ParsedDocument Parse(byte[] pdfBytes);
}
