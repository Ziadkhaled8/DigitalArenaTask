namespace DocConversionService.Application.Tests;

using DocConversionService.Application.Configuration;
using DocConversionService.Application.Interfaces;
using DocConversionService.Application.Jobs;
using DocConversionService.Domain.Entities;
using DocConversionService.Domain.Enums;
using DocConversionService.Domain.Exceptions;
using DocConversionService.Domain.Parsing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using static DocConversionService.Application.Interfaces.IDocumentSplitter;

public class JobOrchestrationServiceTests
{
    private readonly IDocumentParser _parser = Substitute.For<IDocumentParser>();
    private readonly IDocumentRenderer _htmlRenderer = Substitute.For<IDocumentRenderer>();
    private readonly IDocumentRenderer _docxRenderer = Substitute.For<IDocumentRenderer>();
    private readonly IDocumentSplitter _splitter = Substitute.For<IDocumentSplitter>();
    private readonly IJobValidator _validator = Substitute.For<IJobValidator>();
    private readonly IFileStorageProvider _storage = Substitute.For<IFileStorageProvider>();
    private readonly IJobRepository _repository = Substitute.For<IJobRepository>();
    private readonly IOptions<ConversionSettings> _settings = Options.Create(new ConversionSettings { MaxPartSizeBytes = 2 * 1024 * 1024 });
    private readonly ILogger<JobOrchestrationService> _logger = Substitute.For<ILogger<JobOrchestrationService>>();
    private readonly IReadOnlyDictionary<OutputFormat, IDocumentRenderer> _renderers;

    public JobOrchestrationServiceTests()
    {
        _htmlRenderer.Format.Returns(OutputFormat.Html);
        _docxRenderer.Format.Returns(OutputFormat.Docx);

        _renderers = new Dictionary<OutputFormat, IDocumentRenderer>
        {
            [OutputFormat.Html] = _htmlRenderer,
            [OutputFormat.Docx] = _docxRenderer
        };

        _storage.SaveAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(ci.Arg<string>()));
    }

    private JobOrchestrationService CreateService() => new(
        _parser,
        _renderers,
        _splitter,
        _validator,
        _storage,
        _repository,
        _settings,
        _logger);

    [Fact]
    public async Task SubmitAndProcessAsync_SmallDocument_CompletesWithSinglePart()
    {
        // Arrange
        var elements = new List<DocumentElement> { new ParagraphElement("Hello world") };
        var parsedDoc = new ParsedDocument(elements);
        _parser.Parse(Arg.Any<byte[]>()).Returns(parsedDoc);

        var renderedBytes = new byte[1024]; // 1KB <= 2MB limit
        _htmlRenderer.Render(Arg.Any<IReadOnlyList<DocumentElement>>()).Returns(renderedBytes);

        // Splitter is always called; WasSplit=false means no Splitting transition
        var splitResult = new SplitResult(new List<SplitPartResult>
        {
            new(1, 1, renderedBytes, false, elements)
        }, WasSplit: false);
        _splitter.Split(parsedDoc, _htmlRenderer, _settings.Value.MaxPartSizeBytes).Returns(splitResult);

        _validator.Validate(parsedDoc, splitResult.Parts).Returns(new ValidationResult(true, null));

        ConversionJob? savedJob = null;
        await _repository.AddAsync(Arg.Do<ConversionJob>(j => savedJob = j), Arg.Any<CancellationToken>());

        var sut = CreateService();
        var command = new SubmitJobCommand(new byte[] { 1, 2, 3 }, "sample.pdf", OutputFormat.Html);

        // Act
        var jobId = await sut.SubmitAndProcessAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(savedJob);
        Assert.Equal(jobId, savedJob.Id);
        Assert.Equal(JobStatus.Completed, savedJob.Status);
        Assert.Equal(OutputFormat.Html, savedJob.ResolvedFormat);
        Assert.Single(savedJob.Parts);
        Assert.Equal(1, savedJob.Parts.First().PartNumber);
        Assert.Equal(1, savedJob.Parts.First().TotalParts);
        Assert.False(savedJob.Parts.First().ExceedsSizeLimit);

        _splitter.Received(1).Split(parsedDoc, _htmlRenderer, _settings.Value.MaxPartSizeBytes);
        await _storage.Received(2).SaveAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>()); // source + 1 part
    }

    [Fact]
    public async Task SubmitAndProcessAsync_LargeDocument_SplitsIntoMultiplePartsAndCompletes()
    {
        // Arrange
        var elements = new List<DocumentElement>
        {
            new ParagraphElement("Part 1 text"),
            new ParagraphElement("Part 2 text")
        };
        var parsedDoc = new ParsedDocument(elements);
        _parser.Parse(Arg.Any<byte[]>()).Returns(parsedDoc);

        var oversizedBytes = new byte[3 * 1024 * 1024]; // 3MB > 2MB limit
        _htmlRenderer.Render(Arg.Any<IReadOnlyList<DocumentElement>>()).Returns(oversizedBytes);

        var splitResult = new SplitResult(new List<SplitPartResult>
        {
            new(1, 2, new byte[1024], false, new[] { elements[0] }),
            new(2, 2, new byte[1024], false, new[] { elements[1] })
        }, false);
        _splitter.Split(parsedDoc, _htmlRenderer, _settings.Value.MaxPartSizeBytes).Returns(splitResult);

        _validator.Validate(parsedDoc, splitResult.Parts).Returns(new ValidationResult(true, null));

        ConversionJob? savedJob = null;
        await _repository.AddAsync(Arg.Do<ConversionJob>(j => savedJob = j), Arg.Any<CancellationToken>());

        var sut = CreateService();
        var command = new SubmitJobCommand(new byte[] { 1, 2, 3 }, "large.pdf", OutputFormat.Html);

        // Act
        var jobId = await sut.SubmitAndProcessAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(savedJob);
        Assert.Equal(jobId, savedJob.Id);
        Assert.Equal(JobStatus.Completed, savedJob.Status);
        Assert.Equal(2, savedJob.Parts.Count);
        _splitter.Received(1).Split(parsedDoc, _htmlRenderer, _settings.Value.MaxPartSizeBytes);
    }

    [Fact]
    public async Task SubmitAndProcessAsync_DocxRequestedWithImages_FailsFastWithUnsupportedFormat()
    {
        // Arrange: Document has image XObjects, but user requests DOCX
        var elements = new List<DocumentElement>
        {
            new ImageElement(new byte[] { 1 }, "image/png", 1)
        };
        var parsedDoc = new ParsedDocument(elements);
        _parser.Parse(Arg.Any<byte[]>()).Returns(parsedDoc);

        ConversionJob? savedJob = null;
        await _repository.AddAsync(Arg.Do<ConversionJob>(j => savedJob = j), Arg.Any<CancellationToken>());

        var sut = CreateService();
        var command = new SubmitJobCommand(new byte[] { 1, 2, 3 }, "with-images.pdf", OutputFormat.Docx);

        // Act
        var jobId = await sut.SubmitAndProcessAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(savedJob);
        Assert.Equal(JobStatus.Failed, savedJob.Status);
        Assert.Equal(ErrorCode.UnsupportedFormat, savedJob.ErrorCode);
        Assert.Contains("DOCX output is not supported for documents containing images", savedJob.ErrorMessage);

        // Fail-fast verification: no renderer or splitter was ever invoked
        _docxRenderer.DidNotReceiveWithAnyArgs().Render(default!);
        _htmlRenderer.DidNotReceiveWithAnyArgs().Render(default!);
        _splitter.DidNotReceiveWithAnyArgs().Split(default!, default!, default);
    }

    [Fact]
    public async Task SubmitAndProcessAsync_ScannedDocument_TransitionsToFailedWithScannedDocumentError()
    {
        // Arrange
        _parser.Parse(Arg.Any<byte[]>()).Returns(_ => throw new ScannedDocumentException());

        ConversionJob? savedJob = null;
        await _repository.AddAsync(Arg.Do<ConversionJob>(j => savedJob = j), Arg.Any<CancellationToken>());

        var sut = CreateService();
        var command = new SubmitJobCommand(new byte[] { 1, 2, 3 }, "scanned.pdf", OutputFormat.Html);

        // Act
        var jobId = await sut.SubmitAndProcessAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(savedJob);
        Assert.Equal(JobStatus.Failed, savedJob.Status);
        Assert.Equal(ErrorCode.ScannedDocument, savedJob.ErrorCode);
        Assert.NotNull(savedJob.CompletedAt);
    }

    [Fact]
    public async Task SubmitAndProcessAsync_CorruptedFile_TransitionsToFailedWithCorruptedFileError()
    {
        // Arrange
        _parser.Parse(Arg.Any<byte[]>()).Returns(_ => throw new CorruptedFileException("Corrupted PDF"));

        ConversionJob? savedJob = null;
        await _repository.AddAsync(Arg.Do<ConversionJob>(j => savedJob = j), Arg.Any<CancellationToken>());

        var sut = CreateService();
        var command = new SubmitJobCommand(new byte[] { 1, 2, 3 }, "corrupted.pdf", OutputFormat.Html);

        // Act
        var jobId = await sut.SubmitAndProcessAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(savedJob);
        Assert.Equal(JobStatus.Failed, savedJob.Status);
        Assert.Equal(ErrorCode.CorruptedFile, savedJob.ErrorCode);
    }

    [Fact]
    public async Task SubmitAndProcessAsync_EmptyDocument_TransitionsToFailedWithEmptyDocumentError()
    {
        // Arrange
        _parser.Parse(Arg.Any<byte[]>()).Returns(_ => throw new EmptyDocumentException());

        ConversionJob? savedJob = null;
        await _repository.AddAsync(Arg.Do<ConversionJob>(j => savedJob = j), Arg.Any<CancellationToken>());

        var sut = CreateService();
        var command = new SubmitJobCommand(new byte[] { 1, 2, 3 }, "empty.pdf", OutputFormat.Html);

        // Act
        var jobId = await sut.SubmitAndProcessAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(savedJob);
        Assert.Equal(JobStatus.Failed, savedJob.Status);
        Assert.Equal(ErrorCode.EmptyDocument, savedJob.ErrorCode);
    }

    [Fact]
    public async Task SubmitAndProcessAsync_SplitContainsOversizedPart_CompletesWithWarnings()
    {
        // Arrange
        var elements = new List<DocumentElement> { new ParagraphElement("Text") };
        var parsedDoc = new ParsedDocument(elements);
        _parser.Parse(Arg.Any<byte[]>()).Returns(parsedDoc);

        var oversizedBytes = new byte[3 * 1024 * 1024];
        _htmlRenderer.Render(Arg.Any<IReadOnlyList<DocumentElement>>()).Returns(oversizedBytes);

        var splitResult = new SplitResult(new List<SplitPartResult>
        {
            new(1, 1, oversizedBytes, true, elements) // ExceedsSizeLimit = true
        }, false);
        _splitter.Split(parsedDoc, _htmlRenderer, _settings.Value.MaxPartSizeBytes).Returns(splitResult);
        _validator.Validate(parsedDoc, splitResult.Parts).Returns(new ValidationResult(true, null));

        ConversionJob? savedJob = null;
        await _repository.AddAsync(Arg.Do<ConversionJob>(j => savedJob = j), Arg.Any<CancellationToken>());

        var sut = CreateService();
        var command = new SubmitJobCommand(new byte[] { 1 }, "oversized.pdf", OutputFormat.Html);

        // Act
        var jobId = await sut.SubmitAndProcessAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(savedJob);
        Assert.Equal(JobStatus.CompletedWithWarnings, savedJob.Status);
        Assert.True(savedJob.Parts.First().ExceedsSizeLimit);
    }

    [Fact]
    public async Task SubmitAndProcessAsync_ValidationFails_TransitionsToFlaggedForReview()
    {
        // Arrange
        var elements = new List<DocumentElement> { new ParagraphElement("Text") };
        var parsedDoc = new ParsedDocument(elements);
        _parser.Parse(Arg.Any<byte[]>()).Returns(parsedDoc);

        var renderedBytes = new byte[100];
        _htmlRenderer.Render(Arg.Any<IReadOnlyList<DocumentElement>>()).Returns(renderedBytes);

        // Splitter is always called; single part within limit
        var splitResult = new SplitResult(new List<SplitPartResult>
        {
            new(1, 1, renderedBytes, false, elements)
        }, WasSplit: false);
        _splitter.Split(parsedDoc, _htmlRenderer, _settings.Value.MaxPartSizeBytes).Returns(splitResult);

        // Validator detects hash mismatch or sequence anomaly
        _validator.Validate(parsedDoc, splitResult.Parts)
            .Returns(new ValidationResult(false, "Canonical content hash mismatch across parts"));

        ConversionJob? savedJob = null;
        await _repository.AddAsync(Arg.Do<ConversionJob>(j => savedJob = j), Arg.Any<CancellationToken>());

        var sut = CreateService();
        var command = new SubmitJobCommand(new byte[] { 1 }, "doc.pdf", OutputFormat.Html);

        // Act
        var jobId = await sut.SubmitAndProcessAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(savedJob);
        Assert.Equal(JobStatus.FlaggedForReview, savedJob.Status);
        Assert.Equal(ErrorCode.ValidationFailed, savedJob.ErrorCode);
        Assert.Contains("Output validation failed", savedJob.ErrorMessage);
    }

    [Fact]
    public async Task SubmitAndProcessAsync_CancelledToken_TransitionsJobToFailedWithCancellationMessageAndDoesNotThrow()
    {
        // Arrange
        // Use an already-cancelled token to simulate a client that disconnected before
        // processing even started. The very first I/O call (AddAsync) will throw
        // OperationCanceledException when the token is already cancelled.
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        ConversionJob? savedJob = null;
        // AddAsync captures the job so we can inspect it after the method returns.
        // When the token is already cancelled, AddAsync propagates the cancellation.
        _repository
            .AddAsync(Arg.Do<ConversionJob>(j => savedJob = j), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<CancellationToken>().IsCancellationRequested
                ? Task.FromCanceled(ci.Arg<CancellationToken>())
                : Task.CompletedTask);
        // SaveChangesAsync is called post-cancellation with CancellationToken.None so
        // it must succeed (default NSubstitute behaviour returns Task.CompletedTask).

        var sut = CreateService();
        var command = new SubmitJobCommand(new byte[] { 1, 2, 3 }, "sample.pdf", OutputFormat.Html);

        // Act — must NOT throw out of the method
        var jobId = await sut.SubmitAndProcessAsync(command, cts.Token);

        // Assert
        // The job object was captured before the first await, so savedJob is not null.
        Assert.NotNull(savedJob);
        Assert.Equal(JobStatus.Failed, savedJob.Status);
        Assert.Equal(ErrorCode.Cancelled, savedJob.ErrorCode);
        Assert.Contains("cancelled", savedJob.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // The Failed state must actually reach the database.
        await _repository.Received(1).SaveChangesAsync(CancellationToken.None);
    }
}
