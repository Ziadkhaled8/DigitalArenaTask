namespace DocConversionService.Domain.Entities;

public class OutputPart
{
    public Guid Id { get; private set; }
    public Guid JobId { get; private set; }
    public int PartNumber { get; private set; }
    public int TotalParts { get; private set; }
    public string FilePath { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public bool ExceedsSizeLimit { get; private set; }

    private OutputPart() { } // EF Core

    public OutputPart(Guid jobId, int partNumber, int totalParts, string filePath, long sizeBytes, bool exceedsSizeLimit = false)
    {
        Id = Guid.NewGuid();
        JobId = jobId;
        PartNumber = partNumber;
        TotalParts = totalParts;
        FilePath = filePath;
        SizeBytes = sizeBytes;
        ExceedsSizeLimit = exceedsSizeLimit;
    }
}
