using Zephyrus.Application.Exceptions;
using Zephyrus.Core.Interfaces;
using Zephyrus.Core.Jobs;

namespace Zephyrus.Application.Orchestration;

/// <summary>
/// Queues an agent run a person asked for. Agent runs take minutes, so they
/// never run inside the request that asked for them: the request returns once
/// the job is queued, and the feature page follows its progress.
/// </summary>
public sealed class AgentRunRequests
{
    private readonly IJobQueue _jobQueue;
    private readonly IAgentJobRepository _jobs;

    public AgentRunRequests(IJobQueue jobQueue, IAgentJobRepository jobs)
    {
        _jobQueue = jobQueue;
        _jobs = jobs;
    }

    /// <summary>
    /// Queues <paramref name="kind"/> for the feature, unless one of its agent
    /// jobs is still waiting or running: two runs of the same feature would
    /// race to write the same artifacts.
    /// </summary>
    public async Task QueueAsync(Guid featureId, AgentJobKind kind, bool forceRerun, CancellationToken ct = default)
    {
        var active = (await _jobs.GetByFeatureIdAsync(featureId, ct)).FirstOrDefault(j => j.IsActive);
        if (active is not null)
        {
            throw new RequestRejectedException(
                $"The {active.Kind} Agent is already {active.Status.ToString().ToLowerInvariant()} for this feature. " +
                "Wait for it to finish before starting another run.");
        }

        await _jobQueue.EnqueueAsync(new AgentJob(featureId, kind, forceRerun), ct);
    }
}
