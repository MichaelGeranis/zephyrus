using Zephyrus.Core.Entities;

namespace Zephyrus.Core.Interfaces;

/// <summary>
/// Storage for queued agent jobs.
/// </summary>
public interface IAgentJobRepository
{
    Task AddAsync(AgentJobRecord job, CancellationToken ct = default);

    /// <summary>
    /// Claims the oldest claimable job for this worker, or returns <c>null</c>
    /// when there is none. Safe to call from several workers at once: a job is
    /// handed to at most one of them.
    /// </summary>
    Task<AgentJobRecord?> TryClaimNextAsync(DateTime now, TimeSpan lease, CancellationToken ct = default);

    Task UpdateAsync(AgentJobRecord job, CancellationToken ct = default);

    Task<IReadOnlyList<AgentJobRecord>> GetByFeatureIdAsync(Guid featureId, CancellationToken ct = default);
}
