namespace DocConversionService.Application.Jobs;

using DocConversionService.Domain.Enums;

public record SubmitJobCommand(
    byte[] FileContent,
    string FileName,
    OutputFormat RequestedFormat
);
