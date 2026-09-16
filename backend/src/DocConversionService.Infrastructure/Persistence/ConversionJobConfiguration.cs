namespace DocConversionService.Infrastructure.Persistence;

using DocConversionService.Domain.Entities;
using DocConversionService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ConversionJobConfiguration : IEntityTypeConfiguration<ConversionJob>
{
    public void Configure(EntityTypeBuilder<ConversionJob> builder)
    {
        builder.HasKey(j => j.Id);
        builder.Property(j => j.SourceFileName).IsRequired().HasMaxLength(500);
        builder.Property(j => j.SourceFilePath).IsRequired().HasMaxLength(1000);
        builder.Property(j => j.RequestedFormat).HasConversion<string>().HasMaxLength(20);
        builder.Property(j => j.ResolvedFormat).HasConversion<string>().HasMaxLength(20);
        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(j => j.ErrorCode).HasConversion<string>().HasMaxLength(50);
        builder.Property(j => j.ErrorMessage).HasMaxLength(2000);

        builder.HasMany(j => j.Events)
            .WithOne()
            .HasForeignKey(e => e.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(j => j.Parts)
            .WithOne()
            .HasForeignKey(p => p.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
