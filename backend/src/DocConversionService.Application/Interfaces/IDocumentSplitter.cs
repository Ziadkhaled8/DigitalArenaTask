namespace DocConversionService.Application.Interfaces;

using DocConversionService.Domain.Parsing;

public interface IDocumentSplitter
{
    public record SplitResult(IReadOnlyList<SplitPartResult> Parts, bool WasSplit);
    SplitResult Split(ParsedDocument document, IDocumentRenderer renderer, long limitBytes);
}
