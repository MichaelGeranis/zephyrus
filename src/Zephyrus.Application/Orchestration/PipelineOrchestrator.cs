using Microsoft.Extensions.Logging;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Interfaces;
using Zephyrus.Core.Jobs;

namespace Zephyrus.Application.Orchestration;

/// <summary>
/// Deterministic orchestrator — not an AI. Reacts to approval events by
/// queueing the next agent in the pipeline. Agents are stateless; all state
/// lives in the database and GitHub.
/// </summary>
/// <remarks>
/// The orchestrator only ever <em>enqueues</em>. Agent runs are long (minutes,
/// and for the Code Agent one pass per task) so they must not execute inside
/// the approval request that triggered them.
/// </remarks>
public sealed class PipelineOrchestrator
{
    private readonly IJobQueue _jobQueue;
    private readonly ILogger<PipelineOrchestrator> _logger;

    /// <summary>
    /// The trigger map: which pipeline status queues which agent.
    /// A status absent from this map has no follow-up agent.
    /// </summary>
    private static readonly Dictionary<FeatureStatus, AgentJobKind> TriggerMap = new()
    {
        { FeatureStatus.PrdApproved, AgentJobKind.Architect },
        { FeatureStatus.ArchApproved, AgentJobKind.Task },
        { FeatureStatus.TasksApproved, AgentJobKind.Code },
        { FeatureStatus.QaPending, AgentJobKind.Qa },
        { FeatureStatus.QaApproved, AgentJobKind.DevOps },
    };

    public PipelineOrchestrator(
        IJobQueue jobQueue,
        ILogger<PipelineOrchestrator> logger)
    {
        _jobQueue = jobQueue;
        _logger = logger;
    }

    /// <summary>
    /// Called after an artifact is approved and the feature has advanced.
    /// Queues the follow-up agent for the new status, if the trigger map has one.
    /// </summary>
    public async Task OnArtifactApprovedAsync(Guid featureId, FeatureStatus newStatus, CancellationToken ct = default)
    {
        if (!TriggerMap.TryGetValue(newStatus, out var kind))
        {
            _logger.LogDebug(
                "Feature {FeatureId}: no follow-up agent for status {Status}.", featureId, newStatus);
            return;
        }

        await _jobQueue.EnqueueAsync(new AgentJob(featureId, kind), ct);

        _logger.LogInformation(
            "Feature {FeatureId}: status {Status} reached, queued {Kind} agent job.",
            featureId, newStatus, kind);
    }
}
