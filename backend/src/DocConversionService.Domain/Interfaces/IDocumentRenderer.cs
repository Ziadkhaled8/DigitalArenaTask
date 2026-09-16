namespace DocConversionService.Domain.Interfaces;

using DocConversionService.Domain.Enums;
using DocConversionService.Domain.Parsing;

public interface IDocumentRenderer
{
    OutputFormat Format { get; }
    byte[] Render(IReadOnlyList<DocumentElement> elements);
}
