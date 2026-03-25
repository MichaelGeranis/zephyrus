using Zephyrus.Core.Entities;

namespace Zephyrus.Core.Interfaces;

public interface IAgentInvocationRepository
{
    Task<IReadOnlyList<AgentInvocation>> GetByFeatureIdAsync(Guid featureId, CancellationToken ct = default);
    Task<AgentInvocation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(AgentInvocation invocation, CancellationToken ct = default);
}
