using Microsoft.Extensions.Logging;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.UseCases;

/// <summary>
/// Reacts to a deployment status from the code host. A successful deployment is
/// what moves a feature to <see cref="FeatureStatus.Deployed"/> — approving the
/// generated workflow file never did, and never meant anything shipped.
/// </summary>
public sealed class HandleDeploymentStatusUseCase
{
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IFeatureRepository _featureRepository;
    private readonly IPipelineEventRepository _pipelineEventRepository;
    private readonly ILogger<HandleDeploymentStatusUseCase> _logger;

    public HandleDeploymentStatusUseCase(
        IDeploymentRepository deploymentRepository,
        IFeatureRepository featureRepository,
        IPipelineEventRepository pipelineEventRepository,
        ILogger<HandleDeploymentStatusUseCase> logger)
    {
        _deploymentRepository = deploymentRepository;
        _featureRepository = featureRepository;
        _pipelineEventRepository = pipelineEventRepository;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        string sha,
        string state,
        CancellationToken ct = default)
    {
        var deployment = await _deploymentRepository.GetByShaAsync(sha, ct);
        if (deployment is null)
        {
            // A deployment of a commit Zephyrus did not merge — nothing to record.
            _logger.LogDebug("No deployment tracked for {Sha}; ignoring status '{State}'.", sha, state);
            return;
        }

        if (deployment.Status != DeploymentStatus.Pending)
        {
            _logger.LogDebug("Deployment {Sha} is already {Status}; ignoring '{State}'.",
                sha, deployment.Status, state);
            return;
        }

        if (IsFailure(state))
        {
            deployment.MarkFailed();
            await _deploymentRepository.UpdateAsync(deployment, ct);

            _logger.LogWarning("Feature {FeatureId}: deployment of {Sha} failed ('{State}').",
                deployment.FeatureId, sha, state);
            return;
        }

        if (!IsSuccess(state))
        {
            // in_progress, queued, pending — the deployment has not landed yet.
            return;
        }

        deployment.MarkSuccess();
        await _deploymentRepository.UpdateAsync(deployment, ct);

        await AdvanceFeatureIfReadyAsync(deployment, ct);
    }

    /// <summary>
    /// Moves the feature to Deployed, but only from QaApproved — a deployment
    /// that lands before QA sign-off is recorded without advancing the pipeline.
    /// </summary>
    private async Task AdvanceFeatureIfReadyAsync(Deployment deployment, CancellationToken ct)
    {
        var feature = await _featureRepository.GetByIdAsync(deployment.FeatureId, ct);
        if (feature is null)
            return;

        if (feature.Status != FeatureStatus.QaApproved)
        {
            _logger.LogInformation(
                "Feature {FeatureId}: deployment of {Sha} succeeded while status is {Status}; not advancing.",
                feature.Id, deployment.Sha, feature.Status);
            return;
        }

        var fromStatus = feature.Advance();
        await _featureRepository.UpdateAsync(feature, ct);

        await _pipelineEventRepository.AddAsync(
            PipelineEvent.Create(feature.Id, fromStatus, feature.Status, "system"), ct);

        _logger.LogInformation("Feature {FeatureId}: deployed at {Sha}.", feature.Id, deployment.Sha);
    }

    private static bool IsSuccess(string state)
        => string.Equals(state, "success", StringComparison.OrdinalIgnoreCase);

    private static bool IsFailure(string state)
        => string.Equals(state, "failure", StringComparison.OrdinalIgnoreCase)
        || string.Equals(state, "error", StringComparison.OrdinalIgnoreCase);
}
