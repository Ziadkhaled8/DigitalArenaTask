namespace DocConversionService.Domain.Enums;

public enum JobStatus
{
    Received,
    Converting,
    Splitting,
    ValidatingOutput,
    Completed,
    CompletedWithWarnings,
    FlaggedForReview,
    Failed
}
