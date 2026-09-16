namespace DocConversionService.Infrastructure.Persistence;

using DocConversionService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public DbSet<ConversionJob> ConversionJobs => Set<ConversionJob>();
    public DbSet<JobEvent> JobEvents => Set<JobEvent>();
    public DbSet<OutputPart> OutputParts => Set<OutputPart>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ConversionJobConfiguration());
        modelBuilder.ApplyConfiguration(new JobEventConfiguration());
        modelBuilder.ApplyConfiguration(new OutputPartConfiguration());
    }
}
