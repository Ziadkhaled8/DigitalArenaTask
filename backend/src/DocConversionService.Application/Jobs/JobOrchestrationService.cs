namespace DocConversionService.Application.Jobs;

using DocConversionService.Application.Configuration;
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

        try
        {
            // 1. Save source file
            var sourceKey = $"{job.Id}/source/{command.FileName}";
            var sourcePath = await _storage.SaveAsync(sourceKey, command.FileContent);
            job.SourceFilePath = sourcePath;
            await _repository.SaveChangesAsync();

            // 2. Parse
            var parsedDocument = _parser.Parse(command.FileContent);

            // 3. Format router (fail-fast)
            if (command.RequestedFormat == OutputFormat.Docx && parsedDocument.HasImages)
            {
                throw new UnsupportedFormatException(
                    "DOCX output is not supported for documents containing images. Use HTML format instead.");
            }

            job.SetResolvedFormat(command.RequestedFormat);

            // 4. Convert
            job.TransitionTo(JobStatus.Converting, "Converting document to " + command.RequestedFormat + ".");
            await _repository.SaveChangesAsync();

            var renderer = _renderers[command.RequestedFormat];
            var fullRenderedContent = renderer.Render(parsedDocument.Elements);

            // 5. Check if splitting is needed
            if (fullRenderedContent.Length <= _settings.MaxPartSizeBytes)
            {
                // No split needed — single part
                var partKey = $"{job.Id}/parts/part-1.{GetExtension(command.RequestedFormat)}";
                var partPath = await _storage.SaveAsync(partKey, fullRenderedContent);

                job.AddPart(new OutputPart(
                    job.Id, 1, 1, partPath, fullRenderedContent.Length));

                // Validate
                job.TransitionTo(JobStatus.ValidatingOutput, "Validating output integrity.");
                await _repository.SaveChangesAsync();

                var singlePartResult = new SplitPartResult(1, 1, fullRenderedContent, false, parsedDocument.Elements);
                var validationResult = _validator.Validate(parsedDocument, new[] { singlePartResult });

                if (!validationResult.IsValid)
                {
                    job.TransitionTo(JobStatus.FlaggedForReview,
                        $"Output validation failed: {validationResult.FailureReason}",
                        Domain.Enums.ErrorCode.ValidationFailed);
                }
                else
                {
                    job.TransitionTo(JobStatus.Completed, "Job completed successfully.");
                }
            }
            else
            {
                // 6. Split
                job.TransitionTo(JobStatus.Splitting, "Output exceeds size limit. Splitting into parts.");
                await _repository.SaveChangesAsync();

                var splitParts = _splitter.Split(parsedDocument, renderer, _settings.MaxPartSizeBytes);

                foreach (var part in splitParts)
                {
                    var partKey = $"{job.Id}/parts/part-{part.PartNumber}.{GetExtension(command.RequestedFormat)}";
                    var partPath = await _storage.SaveAsync(partKey, part.Content);

                    job.AddPart(new OutputPart(
                        job.Id, part.PartNumber, part.TotalParts,
                        partPath, part.Content.Length, part.ExceedsSizeLimit));
                }

                // 7. Validate
                job.TransitionTo(JobStatus.ValidatingOutput, "Validating output integrity.");
                await _repository.SaveChangesAsync();

                var validationResult = _validator.Validate(parsedDocument, splitParts);

                if (!validationResult.IsValid)
                {
                    job.TransitionTo(JobStatus.FlaggedForReview,
                        $"Output validation failed: {validationResult.FailureReason}",
                        Domain.Enums.ErrorCode.ValidationFailed);
                }
                else if (splitParts.Any(p => p.ExceedsSizeLimit))
                {
                    job.TransitionTo(JobStatus.CompletedWithWarnings,
                        "Job completed, but one or more parts exceed the size limit.");
                }
                else
                {
                    job.TransitionTo(JobStatus.Completed, "Job completed successfully.");
                }
            }
        }
        catch (DocumentProcessingException ex)
        {
            _logger.LogWarning(ex, "Document processing failed for job {JobId}: {ErrorCode}", job.Id, ex.ErrorCode);
            job.TransitionTo(JobStatus.Failed, ex.Message, ex.ErrorCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing job {JobId}", job.Id);
            job.TransitionTo(JobStatus.Failed, "An unexpected error occurred during processing.", Domain.Enums.ErrorCode.Unknown);
        }

        await _repository.SaveChangesAsync();
        return job.Id;
    }

    private static string GetExtension(OutputFormat format) => format switch
    {
        OutputFormat.Html => "html",
        OutputFormat.Docx => "docx",
        _ => "bin"
    };
}
