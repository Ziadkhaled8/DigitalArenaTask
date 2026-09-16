namespace DocConversionService.Domain.Parsing;

public class ParsedDocument
{
    public IReadOnlyList<DocumentElement> Elements { get; }
    public bool HasImages { get; }

    public ParsedDocument(IReadOnlyList<DocumentElement> elements)
    {
        Elements = elements;
        HasImages = elements.Any(e => e is ImageElement);
    }
}
