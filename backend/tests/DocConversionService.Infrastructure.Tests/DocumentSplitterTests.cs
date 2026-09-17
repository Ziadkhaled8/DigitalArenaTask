namespace DocConversionService.Infrastructure.Tests;

using DocConversionService.Application.Interfaces;
using DocConversionService.Domain.Enums;
using DocConversionService.Domain.Parsing;
using DocConversionService.Infrastructure.Splitting;
using NSubstitute;
using Xunit;

public class DocumentSplitterTests
{
    private readonly DocumentSplitter _splitter = new();
    private readonly IDocumentRenderer _renderer = Substitute.For<IDocumentRenderer>();

    public DocumentSplitterTests()
    {
        _renderer.Format.Returns(OutputFormat.Html);
    }

    [Fact]
    public void Split_SmallDocumentWithinLimit_ReturnsSinglePart()
    {
        // Arrange
        var elements = new List<DocumentElement>
        {
            new HeadingElement("Title", 1),
            new ParagraphElement("A short paragraph.")
        };
        var doc = new ParsedDocument(elements);

        // Mock renderer returns 100 bytes for all combinations
        _renderer.Render(Arg.Any<IReadOnlyList<DocumentElement>>())
            .Returns(new byte[100]);

        // Act
        var parts = _splitter.Split(doc, _renderer, limitBytes: 1000);

        // Assert
        Assert.Single(parts);
        var part = parts[0];
        Assert.Equal(1, part.PartNumber);
        Assert.Equal(1, part.TotalParts);
        Assert.False(part.ExceedsSizeLimit);
        Assert.Equal(2, part.Elements.Count);
    }

    [Fact]
    public void Split_DocumentExceedingLimit_SplitsSequentiallyWithoutReordering()
    {
        // Arrange
        var el1 = new HeadingElement("Chapter 1", 1);
        var el2 = new ParagraphElement("Paragraph 1");
        var el3 = new HeadingElement("Chapter 2", 1);
        var el4 = new ParagraphElement("Paragraph 2");

        var doc = new ParsedDocument(new List<DocumentElement> { el1, el2, el3, el4 });

        // Simulate renderer output size: 600 bytes per element
        _renderer.Render(Arg.Any<IReadOnlyList<DocumentElement>>())
            .Returns(ci =>
            {
                var list = ci.Arg<IReadOnlyList<DocumentElement>>();
                return new byte[list.Count * 600];
            });

        // Limit is 1000 bytes. 1 element = 600 (fits), 2 elements = 1200 (exceeds) -> split!
        var parts = _splitter.Split(doc, _renderer, limitBytes: 1000);

        // Assert
        Assert.Equal(4, parts.Count);
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(i + 1, parts[i].PartNumber);
            Assert.Equal(4, parts[i].TotalParts);
            Assert.False(parts[i].ExceedsSizeLimit);
            Assert.Single(parts[i].Elements);
        }

        // Verify preserved order
        Assert.Same(el1, parts[0].Elements[0]);
        Assert.Same(el2, parts[1].Elements[0]);
        Assert.Same(el3, parts[2].Elements[0]);
        Assert.Same(el4, parts[3].Elements[0]);
    }

    [Fact]
    public void Split_SingleOversizedElement_AllowedToExceedLimitAndFlagged()
    {
        // Arrange: A single large image element that alone exceeds the limit
        var hugeImage = new ImageElement(new byte[10000], "image/png", 1);
        var doc = new ParsedDocument(new List<DocumentElement> { hugeImage });

        // Single element renders as 5000 bytes
        _renderer.Render(Arg.Any<IReadOnlyList<DocumentElement>>())
            .Returns(new byte[5000]);

        // Limit is 2000 bytes
        var parts = _splitter.Split(doc, _renderer, limitBytes: 2000);

        // Assert
        Assert.Single(parts);
        var part = parts[0];
        Assert.Equal(1, part.PartNumber);
        Assert.Equal(1, part.TotalParts);
        Assert.True(part.ExceedsSizeLimit);
        Assert.Equal(5000, part.Content.Length);
    }

    [Fact]
    public void Split_PartExactlyAtLimit_DoesNotTriggerUnnecessarySplit()
    {
        // Arrange
        var el1 = new ParagraphElement("Para 1");
        var el2 = new ParagraphElement("Para 2");
        var doc = new ParsedDocument(new List<DocumentElement> { el1, el2 });

        // 2 elements together render as exactly 2000 bytes
        _renderer.Render(Arg.Is<IReadOnlyList<DocumentElement>>(l => l.Count == 1)).Returns(new byte[1000]);
        _renderer.Render(Arg.Is<IReadOnlyList<DocumentElement>>(l => l.Count == 2)).Returns(new byte[2000]);

        // Limit is exactly 2000 bytes
        var parts = _splitter.Split(doc, _renderer, limitBytes: 2000);

        // Assert: boundary rule <= limitBytes means both elements remain in part 1
        Assert.Single(parts);
        Assert.Equal(1, parts[0].PartNumber);
        Assert.Equal(1, parts[0].TotalParts);
        Assert.False(parts[0].ExceedsSizeLimit);
        Assert.Equal(2, parts[0].Elements.Count);
    }

    [Fact]
    public void Split_OversizedElementInMiddleOfDocument_EmitsAsDedicatedPartAndFlagged()
    {
        // Arrange: Para 1 (500B), Huge Image (5000B), Para 2 (400B) with 2000B limit
        var el1 = new ParagraphElement("Para 1");
        var hugeImage = new ImageElement(new byte[10000], "image/png", 1);
        var el2 = new ParagraphElement("Para 2");
        var doc = new ParsedDocument(new List<DocumentElement> { el1, hugeImage, el2 });

        _renderer.Render(Arg.Any<IReadOnlyList<DocumentElement>>())
            .Returns(ci =>
            {
                var elements = ci.Arg<IReadOnlyList<DocumentElement>>();
                int size = 0;
                foreach (var el in elements)
                {
                    if (el == el1) size += 500;
                    else if (el == hugeImage) size += 5000;
                    else if (el == el2) size += 400;
                }
                return new byte[size];
            });

        // Act: 2000 limit
        var parts = _splitter.Split(doc, _renderer, limitBytes: 2000);

        // Assert:
        // Part 1: el1 (500B, ExceedsSizeLimit: false)
        // Part 2: hugeImage (5000B, ExceedsSizeLimit: true)
        // Part 3: el2 (400B, ExceedsSizeLimit: false)
        Assert.Equal(3, parts.Count);

        Assert.Equal(1, parts[0].PartNumber);
        Assert.False(parts[0].ExceedsSizeLimit);
        Assert.Equal(500, parts[0].Content.Length);
        Assert.Single(parts[0].Elements);
        Assert.Same(el1, parts[0].Elements[0]);

        Assert.Equal(2, parts[1].PartNumber);
        Assert.True(parts[1].ExceedsSizeLimit);
        Assert.Equal(5000, parts[1].Content.Length);
        Assert.Single(parts[1].Elements);
        Assert.Same(hugeImage, parts[1].Elements[0]);

        Assert.Equal(3, parts[2].PartNumber);
        Assert.False(parts[2].ExceedsSizeLimit);
        Assert.Equal(400, parts[2].Content.Length);
        Assert.Single(parts[2].Elements);
        Assert.Same(el2, parts[2].Elements[0]);
    }
}
