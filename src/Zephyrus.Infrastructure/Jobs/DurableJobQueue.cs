using System.Diagnostics;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;
using Zephyrus.Core.Jobs;

namespace Zephyrus.Infrastructure.Jobs;

/// <summary>
/// Stores each agent job in the database, where <see cref="AgentJobWorker"/>
/// picks it up. Jobs survive restarts and can be seen by the team.
/// </summary>
public sealed class DurableJobQueue : IJobQueue
{
    private readonly IAgentJobRepository _jobs;
    private readonly AgentJobSignal _signal;
    private readonly TimeProvider _time;

    public DurableJobQueue(IAgentJobRepository jobs, AgentJobSignal signal, TimeProvider time)
    {
        _jobs = jobs;
        _signal = signal;
        _time = time;
    }

    public async ValueTask EnqueueAsync(AgentJob job, CancellationToken ct = default)
    {
        // Activity.Id is the W3C traceparent of the current span, when tracing is on.
        var traceParent = Activity.Current?.IdFormat == ActivityIdFormat.W3C ? Activity.Current.Id : null;
        await _jobs.AddAsync(AgentJobRecord.Create(job, _time.GetUtcNow().UtcDateTime, traceParent), ct);
        _signal.Notify();
    }
}
