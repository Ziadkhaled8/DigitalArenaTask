namespace DocConversionService.Infrastructure.Persistence;

using DocConversionService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class OutputPartConfiguration : IEntityTypeConfiguration<OutputPart>
{
    public void Configure(EntityTypeBuilder<OutputPart> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.FilePath).IsRequired().HasMaxLength(1000);
    }
}
