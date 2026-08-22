using Zephyrus.Core.Interfaces;
using Zephyrus.Core.Jobs;

namespace Zephyrus.Infrastructure.Jobs;

/// <summary>
/// <see cref="IJobQueue"/> that runs the job immediately on the calling thread,
/// inside the caller's scope. Used by tests that assert the pipeline cascade
/// deterministically, without a background thread to wait on.
/// </summary>
/// <remarks>
/// Not for production use — it reintroduces the blocking behaviour the
/// background queue exists to remove.
/// </remarks>
public sealed class InlineJobQueue : IJobQueue
{
    private readonly IAgentJobDispatcher _dispatcher;

    public InlineJobQueue(IAgentJobDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public async ValueTask EnqueueAsync(AgentJob job, CancellationToken ct = default)
        => await _dispatcher.DispatchAsync(job, ct);
}
