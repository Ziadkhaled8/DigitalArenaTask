namespace DocConversionService.Application.Interfaces;

using DocConversionService.Domain.Entities;

public interface IJobRepository
{
    Task<ConversionJob?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<ConversionJob>> GetAllAsync();
    Task AddAsync(ConversionJob job, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
