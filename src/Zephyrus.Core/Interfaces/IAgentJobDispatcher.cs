using Zephyrus.Core.Jobs;

namespace Zephyrus.Core.Interfaces;

/// <summary>
/// Executes a queued <see cref="AgentJob"/> by routing it to the matching
/// use case. Implemented in the Application layer; consumed by the job
/// runner in Infrastructure so that Infrastructure never references Application.
/// </summary>
public interface IAgentJobDispatcher
{
    Task DispatchAsync(AgentJob job, CancellationToken ct = default);
}
