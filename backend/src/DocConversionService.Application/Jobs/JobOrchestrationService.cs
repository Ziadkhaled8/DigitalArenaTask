namespace DocConversionService.Application.Jobs;

using DocConversionService.Application.Configuration;
using DocConversionService.Application.Interfaces;
using DocConversionService.Domain.Entities;
using DocConversionService.Domain.Enums;
using DocConversionService.Domain.Exceptions;
using DocConversionService.Domain.Interfaces;
using DocConversionService.Domain.Parsing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class JobOrchestrationService
{
    private readonly IDocumentParser _parser;
    private readonly IReadOnlyDictionary<OutputFormat, IDocumentRenderer> _renderers;
    private readonly IDocumentSplitter _splitter;
    private readonly IJobValidator _validator;
    private readonly IFileStorageProvider _storage;
    private readonly IJobRepository _repository;
    private readonly ConversionSettings _settings;
    private readonly ILogger<JobOrchestrationService> _logger;

    public JobOrchestrationService(
        IDocumentParser parser,
        IReadOnlyDictionary<OutputFormat, IDocumentRenderer> renderers,
        IDocumentSplitter splitter,
        IJobValidator validator,
        IFileStorageProvider storage,
        IJobRepository repository,
        IOptions<ConversionSettings> settings,
        ILogger<JobOrchestrationService> logger)
    {
        _parser = parser;
        _renderers = renderers;
        _splitter = splitter;
        _validator = validator;
        _storage = storage;
        _repository = repository;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<Guid> SubmitAndProcessAsync(SubmitJobCommand command)
    {
        var job = ConversionJob.Create(command.FileName, command.RequestedFormat);
        await _repository.AddAsync(job);
        await _repository.SaveChangesAsync(); // durability floor — row exists even if everything below throws unexpectedly

        try
        {
            job.SourceFilePath = await _storage.SaveAsync(
                StorageKeys.Source(job.Id, command.FileName), command.FileContent);

            var parsedDocument = ParseAndRouteFormat(command, job);

            job.TransitionTo(JobStatus.Converting, $"Converting document to {command.RequestedFormat}.");
            var renderer = _renderers[command.RequestedFormat];

            var parts = await BuildPartsAsync(job, parsedDocument, renderer, command.RequestedFormat);

            job.TransitionTo(JobStatus.ValidatingOutput, "Validating output integrity.");
            var validationResult = _validator.Validate(parsedDocument, parts);

            var (finalStatus, finalMessage) = ResolveOutcome(validationResult, parts);
            job.TransitionTo(finalStatus, finalMessage,
                finalStatus == JobStatus.FlaggedForReview ? ErrorCode.ValidationFailed : null);
        }
        catch (DocumentProcessingException ex)
        {
            _logger.LogWarning(ex, "Document processing failed for job {JobId}: {ErrorCode}", job.Id, ex.ErrorCode);
            job.TransitionTo(JobStatus.Failed, ex.Message, ex.ErrorCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing job {JobId}", job.Id);
            job.TransitionTo(JobStatus.Failed, "An unexpected error occurred during processing.", ErrorCode.Unknown);
        }

        await _repository.SaveChangesAsync();
        return job.Id;
    }

    /// <summary>
    /// Parses the source PDF and applies the format router: DOCX is rejected up front
    /// if the document contains images, before any rendering work begins.
    /// </summary>
    private ParsedDocument ParseAndRouteFormat(SubmitJobCommand command, ConversionJob job)
    {
        var parsedDocument = _parser.Parse(command.FileContent);

        if (command.RequestedFormat == OutputFormat.Docx && parsedDocument.HasImages)
        {
            throw new UnsupportedFormatException(
                "DOCX output is not supported for documents containing images. Use HTML format instead.");
        }

        job.SetResolvedFormat(command.RequestedFormat);
        return parsedDocument;
    }

    /// <summary>
    /// Delegates to the splitter (which itself decides whether splitting is actually needed)
    /// and persists every resulting part to storage.
    /// </summary>
    private async Task<IReadOnlyList<SplitPartResult>> BuildPartsAsync(
        ConversionJob job, ParsedDocument parsedDocument, IDocumentRenderer renderer, OutputFormat format)
    {
        var isSplitExpected = renderer.Render(parsedDocument.Elements).Length > _settings.MaxPartSizeBytes;
        if (isSplitExpected)
        {
            job.TransitionTo(JobStatus.Splitting, "Output exceeds size limit. Splitting into parts.");
        }

        var parts = _splitter.Split(parsedDocument, renderer, _settings.MaxPartSizeBytes);
        var extension = GetExtension(format);

        foreach (var part in parts)
        {
            var partPath = await _storage.SaveAsync(
                StorageKeys.Part(job.Id, part.PartNumber, extension), part.Content);

            job.AddPart(new OutputPart(
                job.Id, part.PartNumber, part.TotalParts, partPath, part.Content.Length, part.ExceedsSizeLimit));
        }

        return parts;
    }

    /// <summary>
    /// Single source of truth for what a completed pipeline run actually means:
    /// failed validation outranks an oversized part, which outranks a clean success.
    /// </summary>
    private static (JobStatus Status, string Message) ResolveOutcome(
        ValidationResult validation, IReadOnlyList<SplitPartResult> parts)
    {
        if (!validation.IsValid)
            return (JobStatus.FlaggedForReview, $"Output validation failed: {validation.FailureReason}");

        if (parts.Any(p => p.ExceedsSizeLimit))
            return (JobStatus.CompletedWithWarnings, "Job completed, but one or more parts exceed the size limit.");

        return (JobStatus.Completed, "Job completed successfully.");
    }

    private static string GetExtension(OutputFormat format) => format switch
    {
        OutputFormat.Html => "html",
        OutputFormat.Docx => "docx",
        _ => "bin"
    };
}