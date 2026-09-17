namespace DocConversionService.Application.Jobs;

using DocConversionService.Application.Dtos;
using DocConversionService.Application.Interfaces;

public class JobQueryService
{
    private readonly IJobRepository _repository;

    public JobQueryService(IJobRepository repository)
    {
        _repository = repository;
    }

    public async Task<JobSummaryDto?> GetSummaryAsync(Guid id)
    {
        var job = await _repository.GetByIdAsync(id);
        return job?.ToSummaryDto();
    }

    public async Task<JobDetailDto?> GetDetailAsync(Guid id)
    {
        var job = await _repository.GetByIdAsync(id);
        return job?.ToDetailDto();
    }

    public async Task<IReadOnlyList<JobSummaryDto>> GetAllAsync()
    {
        var jobs = await _repository.GetAllAsync();
        return jobs.Select(j => j.ToSummaryDto()).ToList();
    }
}
