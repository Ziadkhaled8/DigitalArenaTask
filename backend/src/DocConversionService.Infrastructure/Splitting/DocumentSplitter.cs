namespace DocConversionService.Infrastructure.Splitting;

using DocConversionService.Application.Interfaces;
using DocConversionService.Domain.Parsing;

public class DocumentSplitter : IDocumentSplitter
{
    public IReadOnlyList<SplitPartResult> Split(ParsedDocument document, IDocumentRenderer renderer, long limitBytes)
    {
        var parts = new List<(List<DocumentElement> Elements, byte[] Content, bool ExceedsSizeLimit)>();
        var currentElements = new List<DocumentElement>();

        foreach (var element in document.Elements)
        {
            currentElements.Add(element);
            var rendered = renderer.Render(currentElements);

            if (rendered.Length > limitBytes)
            {
                if (currentElements.Count == 1)
                {
                    // Single element exceeds limit on its own — allow it but flag
                    parts.Add((new List<DocumentElement>(currentElements), rendered, true));
                    currentElements.Clear();
                }
                else
                {
                    // Remove the element that pushed it over and emit previous elements
                    currentElements.RemoveAt(currentElements.Count - 1);
                    var partContent = renderer.Render(currentElements);
                    parts.Add((new List<DocumentElement>(currentElements), partContent, partContent.Length > limitBytes));

                    // Check if the removed element exceeds the limit on its own
                    var elementContent = renderer.Render(new[] { element });
                    if (elementContent.Length > limitBytes)
                    {
                        // Unsplittable single element exceeds limit — emit it as its own flagged part
                        parts.Add((new List<DocumentElement> { element }, elementContent, true));
                        currentElements.Clear();
                    }
                    else
                    {
                        // Start new part with this element
                        currentElements.Clear();
                        currentElements.Add(element);
                    }
                }
            }
        }

        // Add remaining elements as final part
        if (currentElements.Count > 0)
        {
            var finalContent = renderer.Render(currentElements);
            parts.Add((new List<DocumentElement>(currentElements), finalContent, finalContent.Length > limitBytes));
        }

        int totalParts = parts.Count;
        return parts.Select((p, i) => new SplitPartResult(
            PartNumber: i + 1,
            TotalParts: totalParts,
            Content: p.Content,
            ExceedsSizeLimit: p.ExceedsSizeLimit,
            Elements: p.Elements.AsReadOnly()
        )).ToList();
    }
}
