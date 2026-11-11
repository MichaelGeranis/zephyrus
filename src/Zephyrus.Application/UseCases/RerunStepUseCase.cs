using Zephyrus.Application.Exceptions;
using Zephyrus.Application.Orchestration;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Interfaces;
using Zephyrus.Core.Jobs;

namespace Zephyrus.Application.UseCases;

/// <summary>
/// Queues a re-run of a pipeline step, for a feature stuck after a failed run
/// or whose artifact a person wants regenerated. The run happens in the
/// background like every other agent run.
/// </summary>
public sealed class RerunStepUseCase
{
    private static readonly IReadOnlyDictionary<string, AgentJobKind> Steps =
        new Dictionary<string, AgentJobKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["prd"] = AgentJobKind.Prd,
            ["architect"] = AgentJobKind.Architect,
            ["tasks"] = AgentJobKind.Task,
            ["code"] = AgentJobKind.Code,
            ["qa"] = AgentJobKind.Qa,
            ["devops"] = AgentJobKind.DevOps,
        };

    /// <summary>The step a feature waiting at each status re-runs by default.</summary>
    private static readonly IReadOnlyDictionary<FeatureStatus, AgentJobKind> StepForStatus =
        new Dictionary<FeatureStatus, AgentJobKind>
        {
            [FeatureStatus.PrdPending] = AgentJobKind.Prd,
            [FeatureStatus.ArchPending] = AgentJobKind.Architect,
            [FeatureStatus.TasksPending] = AgentJobKind.Task,
            [FeatureStatus.Coding] = AgentJobKind.Code,
            [FeatureStatus.QaPending] = AgentJobKind.Qa,
            [FeatureStatus.QaApproved] = AgentJobKind.DevOps,
        };

    private readonly IFeatureRepository _featureRepository;
    private readonly AgentRunRequests _agentRuns;

    public RerunStepUseCase(IFeatureRepository featureRepository, AgentRunRequests agentRuns)
    {
        _featureRepository = featureRepository;
        _agentRuns = agentRuns;
    }

    public async Task ExecuteAsync(Guid featureId, string? step = null, CancellationToken ct = default)
    {
        var feature = await _featureRepository.GetByIdAsync(featureId, ct)
            ?? throw new ResourceNotFoundException($"Feature '{featureId}' not found.");

        if (step is not null)
        {
            if (!Steps.TryGetValue(step, out var requested))
            {
                throw new RequestRejectedException(
                    $"Unknown step '{step}'. Valid values: prd, architect, tasks, code, qa, devops.");
            }

            // A named step re-runs even if the feature has moved past it.
            await _agentRuns.QueueAsync(featureId, requested, forceRerun: true, ct);
            return;
        }

        if (!StepForStatus.TryGetValue(feature.Status, out var kind))
        {
            throw new RequestRejectedException(
                $"Feature is in '{feature.Status}' status — there is no step to re-run. " +
                "Re-run is only available for pending or in-progress statuses.");
        }

        await _agentRuns.QueueAsync(featureId, kind, forceRerun: false, ct);
    }
}
