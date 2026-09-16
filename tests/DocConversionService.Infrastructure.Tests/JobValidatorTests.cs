namespace DocConversionService.Infrastructure.Tests;

using DocConversionService.Domain.Parsing;
using DocConversionService.Infrastructure.Validation;
using Xunit;

public class JobValidatorTests
{
    private readonly JobValidator _validator = new();

    [Fact]
    public void Validate_ValidSequenceAndMatchingContent_ReturnsValid()
    {
        // Arrange
        var el1 = new HeadingElement("Chapter 1", 1);
        var el2 = new ParagraphElement("Content 1");
        var el3 = new ParagraphElement("Content 2");
        var original = new ParsedDocument(new List<DocumentElement> { el1, el2, el3 });

        var parts = new List<SplitPartResult>
        {
            new(1, 2, new byte[50], false, new DocumentElement[] { el1, el2 }),
            new(2, 2, new byte[50], false, new DocumentElement[] { el3 })
        };

        // Act
        var result = _validator.Validate(original, parts);

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public void Validate_EmptyPartsList_ReturnsInvalid()
    {
        // Arrange
        var original = new ParsedDocument(new List<DocumentElement> { new ParagraphElement("Content") });
        var parts = new List<SplitPartResult>();

        // Act
        var result = _validator.Validate(original, parts);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("No output parts", result.FailureReason);
    }

    [Fact]
    public void Validate_PartSequenceGap_ReturnsInvalid()
    {
        // Arrange: Part numbers are 1 and 3 (part 2 missing)
        var el1 = new ParagraphElement("Content 1");
        var el2 = new ParagraphElement("Content 2");
        var original = new ParsedDocument(new List<DocumentElement> { el1, el2 });

        var parts = new List<SplitPartResult>
        {
            new(1, 3, new byte[50], false, new[] { el1 }),
            new(3, 3, new byte[50], false, new[] { el2 })
        };

        // Act
        var result = _validator.Validate(original, parts);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_InconsistentTotalParts_ReturnsInvalid()
    {
        // Arrange
        var el1 = new ParagraphElement("Content 1");
        var el2 = new ParagraphElement("Content 2");
        var original = new ParsedDocument(new List<DocumentElement> { el1, el2 });

        var parts = new List<SplitPartResult>
        {
            new(1, 2, new byte[50], false, new[] { el1 }),
            new(2, 3, new byte[50], false, new[] { el2 }) // Inconsistent: 3 instead of 2
        };

        // Act
        var result = _validator.Validate(original, parts);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Inconsistent TotalParts", result.FailureReason);
    }

    [Fact]
    public void Validate_ContentHashMismatch_ReturnsInvalid()
    {
        // Arrange: Split parts contain different/tampered text from original
        var originalEl = new ParagraphElement("Original text");
        var tamperedEl = new ParagraphElement("Tampered modified text");

        var original = new ParsedDocument(new List<DocumentElement> { originalEl });
        var parts = new List<SplitPartResult>
        {
            new(1, 1, new byte[50], false, new[] { tamperedEl })
        };

        // Act
        var result = _validator.Validate(original, parts);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("canonical content hash mismatch", result.FailureReason);
    }
}
