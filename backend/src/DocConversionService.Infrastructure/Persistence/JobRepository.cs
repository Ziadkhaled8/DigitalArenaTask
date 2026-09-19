namespace DocConversionService.Infrastructure.Persistence;

using DocConversionService.Application.Interfaces;
using DocConversionService.Domain.Entities;
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

    public async Task AddAsync(ConversionJob job, CancellationToken cancellationToken = default)
    {
        await _dbContext.ConversionJobs.AddAsync(job, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // EF Core 9 bug workaround: when a new JobEvent or OutputPart is added to
        // a ConversionJob's private backing collection (_events / _parts) via
        // _events.Add(new JobEvent(...)), EF discovers it during DetectChanges and
        // assigns it EntityState.Modified (because its GUID is non-default, which
        // EF interprets as "this entity already exists in the store").
        // This causes a failing UPDATE against a row that was never inserted.
        //
        // JobEvent and OutputPart are immutable/append-only — they are NEVER
        // updated after initial creation. So Modified is always wrong for these
        // types: it always means EF misidentified a brand-new entity. We correct
        // it to Added before persisting.
        _dbContext.ChangeTracker.DetectChanges();

        foreach (var entry in _dbContext.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified
                     && (e.Entity is JobEvent || e.Entity is OutputPart))
            .ToList())
        {
            entry.State = EntityState.Added;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
