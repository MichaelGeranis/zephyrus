using System.Threading.Channels;
using Zephyrus.Core.Interfaces;
using Zephyrus.Core.Jobs;

namespace Zephyrus.Infrastructure.Jobs;

/// <summary>
/// In-process <see cref="IJobQueue"/> backed by an unbounded channel.
/// Enqueuing returns immediately; <see cref="AgentJobWorker"/> drains the
/// channel on a background thread.
/// </summary>
/// <remarks>
/// Jobs live in memory, so anything still queued when the process stops is
/// lost. Because agents are stateless and the feature keeps its status until
/// the agent succeeds, a lost job is recovered by re-running the step rather
/// than by replaying the queue.
/// </remarks>
public sealed class BackgroundJobQueue : IJobQueue
{
    private readonly Channel<AgentJob> _channel =
        Channel.CreateUnbounded<AgentJob>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(AgentJob job, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(job, ct);

    /// <summary>
    /// Consumes queued jobs until the channel completes or <paramref name="ct"/> fires.
    /// </summary>
    public IAsyncEnumerable<AgentJob> ReadAllAsync(CancellationToken ct = default)
        => _channel.Reader.ReadAllAsync(ct);
}
