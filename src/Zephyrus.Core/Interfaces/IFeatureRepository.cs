using Zephyrus.Core.Entities;

namespace Zephyrus.Core.Interfaces;

/// <summary>
/// Repository for Feature aggregate persistence.
/// </summary>
public interface IFeatureRepository
{
    Task<Feature?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Feature>> GetByProjectIdAsync(Guid projectId, CancellationToken ct = default);
    Task AddAsync(Feature feature, CancellationToken ct = default);
    Task UpdateAsync(Feature feature, CancellationToken ct = default);
}
