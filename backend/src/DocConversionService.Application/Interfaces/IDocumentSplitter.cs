namespace DocConversionService.Application.Interfaces;

using DocConversionService.Domain.Parsing;

public interface IDocumentSplitter
{
    IReadOnlyList<SplitPartResult> Split(ParsedDocument document, IDocumentRenderer renderer, long limitBytes);
}
