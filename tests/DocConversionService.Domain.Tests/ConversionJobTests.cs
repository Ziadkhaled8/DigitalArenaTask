namespace DocConversionService.Domain.Tests;

using DocConversionService.Domain.Entities;
using DocConversionService.Domain.Enums;
using Xunit;

public class ConversionJobTests
{
    [Fact]
    public void Create_InitializesJobWithReceivedStatusAndInitialEvent()
    {
        // Act
        var job = ConversionJob.Create("test.pdf", OutputFormat.Html);

        // Assert
        Assert.NotEqual(Guid.Empty, job.Id);
        Assert.Equal("test.pdf", job.SourceFileName);
        Assert.Equal(OutputFormat.Html, job.RequestedFormat);
        Assert.Null(job.ResolvedFormat);
        Assert.Equal(JobStatus.Received, job.Status);
        Assert.Null(job.CompletedAt);
        Assert.Null(job.ErrorCode);
        Assert.Null(job.ErrorMessage);

        var initialEvent = Assert.Single(job.Events);
        Assert.Equal(JobStatus.Received, initialEvent.Status);
        Assert.Equal(job.Id, initialEvent.JobId);
        Assert.Contains("received", initialEvent.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TransitionTo_ValidDirectFlowWithoutSplitting_Succeeds()
    {
        // Arrange
        var job = ConversionJob.Create("doc.pdf", OutputFormat.Html);

        // Act
        job.TransitionTo(JobStatus.Converting, "Converting document");
        job.TransitionTo(JobStatus.ValidatingOutput, "Validating output");
        job.TransitionTo(JobStatus.Completed, "Completed successfully");

        // Assert
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.NotNull(job.CompletedAt);
        Assert.Equal(4, job.Events.Count);
    }

    [Fact]
    public void TransitionTo_ValidSplittingFlow_Succeeds()
    {
        // Arrange
        var job = ConversionJob.Create("doc.pdf", OutputFormat.Docx);

        // Act
        job.TransitionTo(JobStatus.Converting, "Converting");
        job.TransitionTo(JobStatus.Splitting, "Splitting");
        job.TransitionTo(JobStatus.ValidatingOutput, "Validating");
        job.TransitionTo(JobStatus.Completed, "Completed");

        // Assert
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.NotNull(job.CompletedAt);
        Assert.Equal(5, job.Events.Count);
    }

    [Theory]
    [InlineData(JobStatus.CompletedWithWarnings)]
    [InlineData(JobStatus.FlaggedForReview)]
    public void TransitionTo_TerminalStates_SetCompletedAt(JobStatus terminalStatus)
    {
        // Arrange
        var job = ConversionJob.Create("doc.pdf", OutputFormat.Html);
        job.TransitionTo(JobStatus.Converting, "Converting");
        job.TransitionTo(JobStatus.ValidatingOutput, "Validating");

        // Act
        job.TransitionTo(terminalStatus, "Finished", terminalStatus == JobStatus.FlaggedForReview ? ErrorCode.ValidationFailed : null);

        // Assert
        Assert.Equal(terminalStatus, job.Status);
        Assert.NotNull(job.CompletedAt);
    }

    [Fact]
    public void TransitionTo_FailedFromAnyProcessingState_RecordsErrorAndCompletedAt()
    {
        // Arrange
        var job = ConversionJob.Create("doc.pdf", OutputFormat.Docx);
        job.TransitionTo(JobStatus.Converting, "Converting");

        // Act
        job.TransitionTo(JobStatus.Failed, "Corrupted file encountered", ErrorCode.CorruptedFile);

        // Assert
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal(ErrorCode.CorruptedFile, job.ErrorCode);
        Assert.Equal("Corrupted file encountered", job.ErrorMessage);
        Assert.NotNull(job.CompletedAt);

        var lastEvent = job.Events.Last();
        Assert.Equal(JobStatus.Failed, lastEvent.Status);
        Assert.Equal(ErrorCode.CorruptedFile, lastEvent.ErrorCode);
    }

    [Theory]
    [InlineData(JobStatus.Received, JobStatus.Completed)]
    [InlineData(JobStatus.Received, JobStatus.Splitting)]
    [InlineData(JobStatus.Converting, JobStatus.Completed)]
    [InlineData(JobStatus.Splitting, JobStatus.Completed)]
    [InlineData(JobStatus.Splitting, JobStatus.Converting)]
    public void TransitionTo_InvalidTransitions_ThrowsInvalidOperationException(JobStatus from, JobStatus to)
    {
        // Arrange
        var job = ConversionJob.Create("doc.pdf", OutputFormat.Html);
        if (from == JobStatus.Converting)
        {
            job.TransitionTo(JobStatus.Converting, "Converting");
        }
        else if (from == JobStatus.Splitting)
        {
            job.TransitionTo(JobStatus.Converting, "Converting");
            job.TransitionTo(JobStatus.Splitting, "Splitting");
        }

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => job.TransitionTo(to, "Invalid transition"));
        Assert.Contains($"Cannot transition from {from} to {to}", ex.Message);
    }

    [Fact]
    public void SetResolvedFormat_UpdatesResolvedFormatAndTimestamp()
    {
        // Arrange
        var job = ConversionJob.Create("doc.pdf", OutputFormat.Docx);
        var initialUpdatedAt = job.UpdatedAt;

        // Act
        job.SetResolvedFormat(OutputFormat.Docx);

        // Assert
        Assert.Equal(OutputFormat.Docx, job.ResolvedFormat);
        Assert.True(job.UpdatedAt >= initialUpdatedAt);
    }

    [Fact]
    public void AddPart_AppendsPartToJob()
    {
        // Arrange
        var job = ConversionJob.Create("doc.pdf", OutputFormat.Html);
        var part = new OutputPart(job.Id, 1, 2, "storage/part-1.html", 1024, false);

        // Act
        job.AddPart(part);

        // Assert
        var addedPart = Assert.Single(job.Parts);
        Assert.Equal(1, addedPart.PartNumber);
        Assert.Equal(2, addedPart.TotalParts);
        Assert.Equal("storage/part-1.html", addedPart.FilePath);
        Assert.Equal(1024, addedPart.SizeBytes);
        Assert.False(addedPart.ExceedsSizeLimit);
    }
}
