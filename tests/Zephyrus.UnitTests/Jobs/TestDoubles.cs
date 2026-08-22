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
