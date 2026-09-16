namespace DocConversionService.Domain.Parsing;

public record SplitPartResult(
    int PartNumber,
    int TotalParts,
    byte[] Content,
    bool ExceedsSizeLimit,
    IReadOnlyList<DocumentElement> Elements
);
