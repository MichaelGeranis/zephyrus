using Zephyrus.Application.Exceptions;
using Zephyrus.Application.Orchestration;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Interfaces;
using Zephyrus.Core.Jobs;

namespace Zephyrus.Application.UseCases;

/// <summary>
/// A PM/EM asks for the PRD of a feature idea. Checks the request can be
/// served, then queues the PRD Agent; <see cref="InvokePrdAgentUseCase"/> runs
/// it in the background.
/// </summary>
public sealed class QueuePrdAgentUseCase
{
    private readonly IFeatureRepository _featureRepository;
    private readonly AgentRunRequests _agentRuns;

    public QueuePrdAgentUseCase(IFeatureRepository featureRepository, AgentRunRequests agentRuns)
    {
        _featureRepository = featureRepository;
        _agentRuns = agentRuns;
    }

    public async Task ExecuteAsync(Guid featureId, CancellationToken ct = default)
    {
        var feature = await _featureRepository.GetByIdAsync(featureId, ct)
            ?? throw new ResourceNotFoundException($"Feature '{featureId}' not found.");

        if (feature.Status is not (FeatureStatus.Ideation or FeatureStatus.PrdPending))
        {
            throw new RequestRejectedException(
                $"Feature must be in Ideation or PrdPending status to generate PRD. Current status: {feature.Status}.");
        }

        await _agentRuns.QueueAsync(featureId, AgentJobKind.Prd, forceRerun: false, ct);
    }
}
