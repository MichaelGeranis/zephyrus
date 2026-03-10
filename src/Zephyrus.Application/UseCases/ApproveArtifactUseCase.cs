using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Exceptions;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.UseCases;

/// <summary>
/// Approves an artifact and advances the feature through the pipeline.
/// This is the reusable approval gate — works for PRD, ADR, Tasks, etc.
/// </summary>
public sealed class ApproveArtifactUseCase
{
    private readonly IFeatureRepository _featureRepository;
    private readonly IArtifactRepository _artifactRepository;
    private readonly IPipelineEventRepository _pipelineEventRepository;

    /// <summary>
    /// Maps each artifact type to the feature status that must be current
    /// for approval to be valid. E.g., a PRD can only be approved when the
    /// feature is in PrdPending (the PRD has been generated and awaits review).
    /// </summary>
    private static readonly Dictionary<ArtifactType, FeatureStatus> ApprovalPreconditions = new()
    {
        { ArtifactType.Prd, FeatureStatus.PrdPending },
        { ArtifactType.Adr, FeatureStatus.ArchPending },
        { ArtifactType.Task, FeatureStatus.TasksPending },
        { ArtifactType.Pr, FeatureStatus.Coding },
        { ArtifactType.Test, FeatureStatus.QaPending },
    };

    public ApproveArtifactUseCase(
        IFeatureRepository featureRepository,
        IArtifactRepository artifactRepository,
        IPipelineEventRepository pipelineEventRepository)
    {
        _featureRepository = featureRepository;
        _artifactRepository = artifactRepository;
        _pipelineEventRepository = pipelineEventRepository;
    }

    public async Task<Artifact> ExecuteAsync(
        Guid featureId,
        Guid artifactId,
        string approvedBy,
        CancellationToken ct = default)
    {
        var feature = await _featureRepository.GetByIdAsync(featureId, ct)
            ?? throw new InvalidOperationException($"Feature '{featureId}' not found.");

        var artifact = await _artifactRepository.GetByIdAsync(artifactId, ct)
            ?? throw new ArtifactNotFoundException(artifactId);

        if (artifact.FeatureId != featureId)
        {
            throw new InvalidOperationException(
                $"Artifact '{artifactId}' does not belong to feature '{featureId}'.");
        }

        if (artifact.ApprovedAt.HasValue)
        {
            throw new InvalidOperationException(
                $"Artifact '{artifactId}' has already been approved by '{artifact.ApprovedBy}' at {artifact.ApprovedAt}.");
        }

        if (!ApprovalPreconditions.TryGetValue(artifact.Type, out var requiredStatus))
        {
            throw new InvalidOperationException(
                $"Artifact type '{artifact.Type}' does not support approval.");
        }

        if (feature.Status != requiredStatus)
        {
            throw new InvalidOperationException(
                $"Feature must be in '{requiredStatus}' status to approve a {artifact.Type} artifact. Current status: '{feature.Status}'.");
        }

        // Mark artifact approved
        artifact.Approve(approvedBy);
        await _artifactRepository.UpdateAsync(artifact, ct);

        // Advance pipeline: e.g. PrdPending → PrdApproved
        var fromStatus = feature.Advance();
        await _featureRepository.UpdateAsync(feature, ct);

        // Record audit event
        await _pipelineEventRepository.AddAsync(
            PipelineEvent.Create(featureId, fromStatus, feature.Status, approvedBy), ct);

        return artifact;
    }
}
