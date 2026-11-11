using Zephyrus.Core.Jobs;

namespace Zephyrus.Core.Interfaces;

/// <summary>
/// Queues agent work for execution outside the originating request.
/// Enqueuing must be fast and must not run the agent inline.
/// </summary>
public interface IJobQueue
{
    /// <summary>
    /// Schedules <paramref name="job"/> for execution.
    /// </summary>
    ValueTask EnqueueAsync(AgentJob job, CancellationToken ct = default);
}
