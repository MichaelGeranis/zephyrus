using Zephyrus.Core.Entities;

namespace Zephyrus.Core.Interfaces;

/// <summary>
/// Repository for PipelineEvent persistence.
/// </summary>
public interface IPipelineEventRepository
{
    Task<IReadOnlyList<PipelineEvent>> GetByFeatureIdAsync(Guid featureId, CancellationToken ct = default);
    Task AddAsync(PipelineEvent pipelineEvent, CancellationToken ct = default);
}
