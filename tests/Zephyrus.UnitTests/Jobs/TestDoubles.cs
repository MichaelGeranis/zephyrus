using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;
using Zephyrus.Core.Jobs;

namespace Zephyrus.UnitTests.Jobs;

/// <summary>
/// <see cref="IJobQueue"/> that records what was enqueued instead of running it.
/// </summary>
public sealed class RecordingJobQueue : IJobQueue
{
    public List<AgentJob> Enqueued { get; } = new();

    public ValueTask EnqueueAsync(AgentJob job, CancellationToken ct = default)
    {
        Enqueued.Add(job);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// <see cref="IAgentJobDispatcher"/> that records dispatched jobs and can be
/// told to throw, so worker error handling is testable.
/// </summary>
public sealed class RecordingJobDispatcher : IAgentJobDispatcher
{
    private readonly TaskCompletionSource _allDispatched = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly int _expectedCount;

    public RecordingJobDispatcher(int expectedCount = 1)
    {
        _expectedCount = expectedCount;
    }

    public List<AgentJob> Dispatched { get; } = new();

    /// <summary>Job kinds that should throw when dispatched.</summary>
    public HashSet<AgentJobKind> FailOn { get; } = new();

    /// <summary>Completes once <c>expectedCount</c> jobs have been dispatched.</summary>
    public Task AllDispatched => _allDispatched.Task;

    public Task DispatchAsync(AgentJob job, CancellationToken ct = default)
    {
        lock (Dispatched)
        {
            Dispatched.Add(job);
            if (Dispatched.Count >= _expectedCount)
                _allDispatched.TrySetResult();
        }

        if (FailOn.Contains(job.Kind))
            throw new InvalidOperationException($"Simulated failure for {job.Kind}.");

        return Task.CompletedTask;
    }
}

/// <summary>A clock the test moves by hand.</summary>
public sealed class ManualTimeProvider : TimeProvider
{
    public ManualTimeProvider(DateTime start) => Now = start;

    public DateTime Now { get; private set; }

    public void Advance(TimeSpan by) => Now += by;

    public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
}

/// <summary>
/// In-memory <see cref="IAgentJobRepository"/>. Holds the same objects the
/// test created, so assertions see the worker's changes directly.
/// </summary>
public sealed class InMemoryAgentJobRepository : IAgentJobRepository
{
    private readonly List<AgentJobRecord> _jobs = new();

    public Task AddAsync(AgentJobRecord job, CancellationToken ct = default)
    {
        lock (_jobs) _jobs.Add(job);
        return Task.CompletedTask;
    }

    public Task<AgentJobRecord?> TryClaimNextAsync(DateTime now, TimeSpan lease, CancellationToken ct = default)
    {
        lock (_jobs)
        {
            foreach (var job in _jobs.OrderBy(j => j.EnqueuedAt))
            {
                if (job.TryClaim(now, lease))
                    return Task.FromResult<AgentJobRecord?>(job);
            }
        }

        return Task.FromResult<AgentJobRecord?>(null);
    }

    public Task UpdateAsync(AgentJobRecord job, CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<AgentJobRecord>> GetByFeatureIdAsync(Guid featureId, CancellationToken ct = default)
    {
        lock (_jobs)
            return Task.FromResult<IReadOnlyList<AgentJobRecord>>(_jobs.Where(j => j.FeatureId == featureId).ToList());
    }
}
