using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Exceptions;
using Zephyrus.Core.Interfaces;
using Zephyrus.Core.Pipeline;
using Zephyrus.Application.Exceptions;
using Zephyrus.Application.Orchestration;

namespace Zephyrus.Application.UseCases;

/// <summary>
/// Approves an artifact and advances the feature through the pipeline.
/// This is the reusable approval gate — works for PRD, ADR, Tasks, etc.
/// After approval, delegates to the PipelineOrchestrator to trigger the next agent.
/// The approver is taken from the authenticated caller, never from the request.
/// </summary>
public sealed class ApproveArtifactUseCase
{
    private readonly IFeatureRepository _featureRepository;
    private readonly IArtifactRepository _artifactRepository;
    private readonly IPipelineEventRepository _pipelineEventRepository;
    private readonly PipelineOrchestrator _orchestrator;
    private readonly IUserContext _userContext;

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
        { ArtifactType.Workflow, FeatureStatus.QaApproved },
    };

    public ApproveArtifactUseCase(
        IFeatureRepository featureRepository,
        IArtifactRepository artifactRepository,
        IPipelineEventRepository pipelineEventRepository,
        PipelineOrchestrator orchestrator,
        IUserContext userContext)
    {
        _featureRepository = featureRepository;
        _artifactRepository = artifactRepository;
        _pipelineEventRepository = pipelineEventRepository;
        _orchestrator = orchestrator;
        _userContext = userContext;
    }

    public async Task<Artifact> ExecuteAsync(
        Guid featureId,
        Guid artifactId,
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

        // Who the caller is, and whether their roles may approve this artifact type.
        // Identity comes from the authenticated principal so it cannot be forged,
        // which is also what makes the PipelineEvent audit trail trustworthy.
        if (!_userContext.IsAuthenticated || string.IsNullOrWhiteSpace(_userContext.UserId))
            throw UnauthorizedApprovalException.NotAuthenticated(artifact.Type);

        if (!ApprovalAuthority.CanApprove(artifact.Type, _userContext.Roles))
        {
            throw UnauthorizedApprovalException.WrongRole(
                artifact.Type, ApprovalAuthority.RolesFor(artifact.Type), _userContext.Roles);
        }

        var approvedBy = _userContext.UserId;

        var isPastRequiredStatus = feature.Status > requiredStatus;

        if (!isPastRequiredStatus && feature.Status != requiredStatus)
        {
            throw new InvalidOperationException(
                $"Feature must be in '{requiredStatus}' status to approve a {artifact.Type} artifact. Current status: '{feature.Status}'.");
        }

        // Mark artifact approved
        artifact.Approve(approvedBy);
        await _artifactRepository.UpdateAsync(artifact, ct);

        // If the pipeline has already advanced past this step (force-rerun scenario),
        // only mark the artifact — do not re-advance the pipeline or re-trigger the orchestrator.
        if (isPastRequiredStatus)
            return artifact;

        // Advance pipeline: e.g. PrdPending → PrdApproved
        var fromStatus = feature.Advance();
        await _featureRepository.UpdateAsync(feature, ct);

        // Record audit event
        await _pipelineEventRepository.AddAsync(
            PipelineEvent.Create(featureId, fromStatus, feature.Status, approvedBy), ct);

        // Trigger the next agent in the pipeline (if any)
        await _orchestrator.OnArtifactApprovedAsync(featureId, feature.Status, ct);

        return artifact;
    }
}
