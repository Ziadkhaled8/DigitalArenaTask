namespace DocConversionService.Infrastructure.Persistence;

using DocConversionService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class JobEventConfiguration : IEntityTypeConfiguration<JobEvent>
{
    public void Configure(EntityTypeBuilder<JobEvent> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.ErrorCode).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Message).IsRequired().HasMaxLength(2000);
    }
}
