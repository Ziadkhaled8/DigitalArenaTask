namespace DocConversionService.Api.Controllers;

using DocConversionService.Application.Dtos;
using DocConversionService.Application.Interfaces;
using DocConversionService.Application.Jobs;
using DocConversionService.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly JobOrchestrationService _orchestrationService;
    private readonly JobQueryService _queryService;
    private readonly IFileStorageProvider _storageProvider;

    public JobsController(
        JobOrchestrationService orchestrationService,
        JobQueryService queryService,
        IFileStorageProvider storageProvider)
    {
        _orchestrationService = orchestrationService;
        _queryService = queryService;
        _storageProvider = storageProvider;
    }

    [HttpPost]
    [RequestSizeLimit(100_000_000)] // 100MB limit
    public async Task<IActionResult> SubmitJob(IFormFile file, [FromForm] OutputFormat requestedFormat)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("File is required.");
        }

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        var command = new SubmitJobCommand(memoryStream.ToArray(), file.FileName, requestedFormat);

        var jobId = await _orchestrationService.SubmitAndProcessAsync(command);
        var summary = await _queryService.GetSummaryAsync(jobId);

        return Accepted($"/api/jobs/{jobId}", summary);
    }

    [HttpGet]
    public async Task<IActionResult> GetJobs()
    {
        var jobs = await _queryService.GetAllAsync();
        return Ok(jobs);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetJobDetail(Guid id)
    {
        var job = await _queryService.GetDetailAsync(id);
        if (job == null) return NotFound();

        return Ok(job);
    }

    [HttpGet("{id:guid}/parts/{partNumber:int}/download")]
    public async Task<IActionResult> DownloadPart(Guid id, int partNumber)
    {
        var job = await _queryService.GetDetailAsync(id);
        if (job == null) return NotFound();

        var part = job.Parts.FirstOrDefault(p => p.PartNumber == partNumber);
        if (part == null) return NotFound();

        var ext = job.ResolvedFormat?.ToLower() == "html" ? "html" : "docx";
        var partKey = $"{id}/parts/part-{partNumber}.{ext}";

        try
        {
            var content = await _storageProvider.GetAsync(partKey);
            return File(content, "application/octet-stream", $"part-{partNumber}.{ext}");
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
    }
}
