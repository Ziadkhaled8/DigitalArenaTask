namespace DocConversionService.Application.Interfaces;

using DocConversionService.Domain.Parsing;

public interface IDocumentParser
{
    ParsedDocument Parse(byte[] pdfBytes);
}
