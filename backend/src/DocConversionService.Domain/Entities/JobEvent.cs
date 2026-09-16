namespace DocConversionService.Domain.Entities;

using DocConversionService.Domain.Enums;

public class JobEvent
{
    public Guid Id { get; private set; }
    public Guid JobId { get; private set; }
    public JobStatus Status { get; private set; }
    public DateTime Timestamp { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public ErrorCode? ErrorCode { get; private set; }

    private JobEvent() { } // EF Core

    public JobEvent(Guid jobId, JobStatus status, string message, ErrorCode? errorCode = null)
    {
        Id = Guid.NewGuid();
        JobId = jobId;
        Status = status;
        Timestamp = DateTime.UtcNow;
        Message = message;
        ErrorCode = errorCode;
    }
}
