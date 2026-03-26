using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;

namespace Zephyrus.Core.Interfaces;

/// <summary>
/// Repository for Artifact persistence.
/// </summary>
public interface IArtifactRepository
{
    Task<Artifact?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Artifact>> GetByFeatureIdAsync(Guid featureId, CancellationToken ct = default);
    Task<Artifact?> GetByFeatureIdAndTypeAsync(Guid featureId, ArtifactType type, CancellationToken ct = default);
    Task AddAsync(Artifact artifact, CancellationToken ct = default);
    Task UpdateAsync(Artifact artifact, CancellationToken ct = default);
    Task DeleteAsync(Artifact artifact, CancellationToken ct = default);
}
