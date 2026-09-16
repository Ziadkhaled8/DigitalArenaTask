namespace DocConversionService.Infrastructure.Persistence;

using DocConversionService.Domain.Entities;
using DocConversionService.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

public class JobRepository : IJobRepository
{
    private readonly AppDbContext _dbContext;

    public JobRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ConversionJob?> GetByIdAsync(Guid id)
    {
        return await _dbContext.ConversionJobs
            .Include(j => j.Events)
            .Include(j => j.Parts)
            .FirstOrDefaultAsync(j => j.Id == id);
    }

    public async Task<IReadOnlyList<ConversionJob>> GetAllAsync()
    {
        return await _dbContext.ConversionJobs
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(ConversionJob job)
    {
        await _dbContext.ConversionJobs.AddAsync(job);
    }

    public async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }
}
