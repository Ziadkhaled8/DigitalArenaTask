namespace DocConversionService.Application.Interfaces;

using DocConversionService.Domain.Parsing;

public interface IJobValidator
{
    ValidationResult Validate(ParsedDocument original, IReadOnlyList<SplitPartResult> parts);
}
