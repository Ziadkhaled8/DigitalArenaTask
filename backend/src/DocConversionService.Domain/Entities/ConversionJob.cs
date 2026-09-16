namespace DocConversionService.Domain.Entities;

using DocConversionService.Domain.Enums;

public class ConversionJob
{
    private static readonly Dictionary<JobStatus, HashSet<JobStatus>> ValidTransitions = new()
    {
        [JobStatus.Received] = new() { JobStatus.Converting, JobStatus.Failed },
        [JobStatus.Converting] = new() { JobStatus.Splitting, JobStatus.ValidatingOutput, JobStatus.Failed },
        [JobStatus.Splitting] = new() { JobStatus.ValidatingOutput, JobStatus.Failed },
        [JobStatus.ValidatingOutput] = new() { JobStatus.Completed, JobStatus.CompletedWithWarnings, JobStatus.FlaggedForReview, JobStatus.Failed },
    };

    public Guid Id { get; private set; }
    public string SourceFileName { get; private set; } = string.Empty;
    public OutputFormat RequestedFormat { get; private set; }
    public OutputFormat? ResolvedFormat { get; private set; }
    public JobStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string SourceFilePath { get; set; } = string.Empty;
    public ErrorCode? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    private readonly List<JobEvent> _events = new();
    public IReadOnlyCollection<JobEvent> Events => _events.AsReadOnly();

    private readonly List<OutputPart> _parts = new();
    public IReadOnlyCollection<OutputPart> Parts => _parts.AsReadOnly();

    private ConversionJob() { } // EF Core

    public static ConversionJob Create(string sourceFileName, OutputFormat requestedFormat)
    {
        var job = new ConversionJob
        {
            Id = Guid.NewGuid(),
            SourceFileName = sourceFileName,
            RequestedFormat = requestedFormat,
            Status = JobStatus.Received,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        job._events.Add(new JobEvent(
            job.Id,
            JobStatus.Received,
            "Job received and queued for processing."
        ));

        return job;
    }

    public void TransitionTo(JobStatus newStatus, string message, ErrorCode? errorCode = null)
    {
        if (!ValidTransitions.TryGetValue(Status, out var allowed) || !allowed.Contains(newStatus))
        {
            throw new InvalidOperationException(
                $"Cannot transition from {Status} to {newStatus}.");
        }

        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
        ErrorCode = errorCode;
        if (errorCode.HasValue)
            ErrorMessage = message;

        if (newStatus is JobStatus.Completed or JobStatus.CompletedWithWarnings
            or JobStatus.FlaggedForReview or JobStatus.Failed)
        {
            CompletedAt = DateTime.UtcNow;
        }

        _events.Add(new JobEvent(Id, newStatus, message, errorCode));
    }

    public void SetResolvedFormat(OutputFormat format)
    {
        ResolvedFormat = format;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddPart(OutputPart part)
    {
        _parts.Add(part);
        UpdatedAt = DateTime.UtcNow;
    }
}
