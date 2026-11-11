using Zephyrus.Core.Entities;

namespace Zephyrus.Core.Interfaces;

/// <summary>
/// Repository for Deployment persistence — the record of which commit went to
/// which environment, and whether it got there.
/// </summary>
public interface IDeploymentRepository
{
    Task<Deployment?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// The deployment recorded for a commit, if any. A commit is deployed to at
    /// most one environment per feature in the current model.
    /// </summary>
    Task<Deployment?> GetByShaAsync(string sha, CancellationToken ct = default);

    Task<IReadOnlyList<Deployment>> GetByFeatureIdAsync(Guid featureId, CancellationToken ct = default);

    Task AddAsync(Deployment deployment, CancellationToken ct = default);

    Task UpdateAsync(Deployment deployment, CancellationToken ct = default);
}
