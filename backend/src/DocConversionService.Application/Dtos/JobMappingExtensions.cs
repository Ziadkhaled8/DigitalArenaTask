namespace DocConversionService.Application.Dtos;

using DocConversionService.Domain.Entities;

public static class JobMappingExtensions
{
    public static JobSummaryDto ToSummaryDto(this ConversionJob job) => new(
        job.Id,
        job.SourceFileName,
        job.RequestedFormat.ToString(),
        job.ResolvedFormat?.ToString(),
        job.Status.ToString(),
        job.CreatedAt,
        job.CompletedAt
    );

    public static JobDetailDto ToDetailDto(this ConversionJob job) => new(
        job.Id,
        job.SourceFileName,
        job.RequestedFormat.ToString(),
        job.ResolvedFormat?.ToString(),
        job.Status.ToString(),
        job.CreatedAt,
        job.CompletedAt,
        job.ErrorCode?.ToString(),
        job.ErrorMessage,
        job.Events.OrderBy(e => e.Timestamp).Select(e => e.ToDto()).ToList(),
        job.Parts.OrderBy(p => p.PartNumber).Select(p => p.ToDto(job.Id)).ToList()
    );

    public static JobEventDto ToDto(this JobEvent e) => new(
        e.Status.ToString(),
        e.Timestamp,
        e.Message,
        e.ErrorCode?.ToString()
    );

    public static OutputPartDto ToDto(this OutputPart p, Guid jobId) => new(
        p.PartNumber,
        p.TotalParts,
        p.SizeBytes,
        p.ExceedsSizeLimit,
        $"/api/jobs/{jobId}/parts/{p.PartNumber}/download"
    );
}
