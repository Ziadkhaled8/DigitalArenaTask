namespace DocConversionService.Application.Tests;

using DocConversionService.Application.Jobs;
using DocConversionService.Domain.Entities;
using DocConversionService.Domain.Enums;
using DocConversionService.Domain.Interfaces;
using NSubstitute;
using Xunit;

public class JobQueryServiceTests
{
    private readonly IJobRepository _repository = Substitute.For<IJobRepository>();

    [Fact]
    public async Task GetSummaryAsync_ExistingJob_ReturnsMappedSummary()
    {
        // Arrange
        var job = ConversionJob.Create("report.pdf", OutputFormat.Html);
        job.SetResolvedFormat(OutputFormat.Html);
        _repository.GetByIdAsync(job.Id).Returns(job);

        var sut = new JobQueryService(_repository);

        // Act
        var result = await sut.GetSummaryAsync(job.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(job.Id, result.Id);
        Assert.Equal("report.pdf", result.SourceFileName);
        Assert.Equal("Html", result.RequestedFormat);
        Assert.Equal("Html", result.ResolvedFormat);
        Assert.Equal("Received", result.Status);
    }

    [Fact]
    public async Task GetSummaryAsync_NonExistingJob_ReturnsNull()
    {
        // Arrange
        _repository.GetByIdAsync(Arg.Any<Guid>()).Returns((ConversionJob?)null);
        var sut = new JobQueryService(_repository);

        // Act
        var result = await sut.GetSummaryAsync(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetDetailAsync_ExistingJobWithEventsAndParts_ReturnsMappedDetail()
    {
        // Arrange
        var job = ConversionJob.Create("statement.pdf", OutputFormat.Docx);
        job.TransitionTo(JobStatus.Converting, "Converting statement");
        job.AddPart(new OutputPart(job.Id, 1, 1, "path/part-1.docx", 2048, false));
        _repository.GetByIdAsync(job.Id).Returns(job);

        var sut = new JobQueryService(_repository);

        // Act
        var result = await sut.GetDetailAsync(job.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(job.Id, result.Id);
        Assert.Equal(2, result.Events.Count);
        Assert.Single(result.Parts);
        Assert.Equal(1, result.Parts.First().PartNumber);
        Assert.Equal(2048, result.Parts.First().SizeBytes);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllSummaries()
    {
        // Arrange
        var job1 = ConversionJob.Create("a.pdf", OutputFormat.Html);
        var job2 = ConversionJob.Create("b.pdf", OutputFormat.Docx);
        _repository.GetAllAsync().Returns(new List<ConversionJob> { job1, job2 });

        var sut = new JobQueryService(_repository);

        // Act
        var results = await sut.GetAllAsync();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.SourceFileName == "a.pdf");
        Assert.Contains(results, r => r.SourceFileName == "b.pdf");
    }
}
