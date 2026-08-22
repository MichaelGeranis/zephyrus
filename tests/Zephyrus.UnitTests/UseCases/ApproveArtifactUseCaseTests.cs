using Microsoft.Extensions.Logging.Abstractions;
using Zephyrus.Application.Orchestration;
using Zephyrus.Application.UseCases;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;
using Zephyrus.Application.Exceptions;
using Zephyrus.Core.Exceptions;
using Zephyrus.UnitTests.Jobs;

namespace Zephyrus.UnitTests.UseCases;

public class ApproveArtifactUseCaseTests
{
    private readonly InMemoryFeatureRepository _featureRepo = new();
    private readonly InMemoryArtifactRepository _artifactRepo = new();
    private readonly InMemoryPipelineEventRepository _eventRepo = new();
    private readonly FakeUserContext _userContext = new();
    private readonly ApproveArtifactUseCase _sut;

    public ApproveArtifactUseCaseTests()
    {
        // The orchestrator only queues follow-up agent work, so a recording queue
        // is enough here — agents are exercised by the integration tests.
        var orchestrator = new PipelineOrchestrator(
            new RecordingJobQueue(), NullLogger<PipelineOrchestrator>.Instance);

        _sut = new ApproveArtifactUseCase(_featureRepo, _artifactRepo, _eventRepo, orchestrator, _userContext);
    }

    private static Feature CreateFeatureAt(Guid projectId, FeatureStatus targetStatus)
    {
        var feature = Feature.Create(projectId, "prompt");
        while (feature.Status != targetStatus)
            feature.Advance();
        return feature;
    }

    // ── Error cases ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenFeatureNotFound_ThrowsInvalidOperationException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task ExecuteAsync_WhenArtifactNotFound_ThrowsArtifactNotFoundException()
    {
        var feature = Feature.Create(Guid.NewGuid(), "prompt");
        await _featureRepo.AddAsync(feature);

        await Assert.ThrowsAsync<ArtifactNotFoundException>(() =>
            _sut.ExecuteAsync(feature.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task ExecuteAsync_WhenArtifactBelongsToDifferentFeature_ThrowsInvalidOperationException()
    {
        var feature = Feature.Create(Guid.NewGuid(), "prompt");
        await _featureRepo.AddAsync(feature);

        var artifact = Artifact.Create(Guid.NewGuid(), ArtifactType.Prd);
        await _artifactRepo.AddAsync(artifact);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ExecuteAsync(feature.Id, artifact.Id));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAlreadyApproved_ThrowsInvalidOperationException()
    {
        var feature = CreateFeatureAt(Guid.NewGuid(), FeatureStatus.PrdPending);
        await _featureRepo.AddAsync(feature);

        var artifact = Artifact.Create(feature.Id, ArtifactType.Prd);
        artifact.Approve("first-approver");
        await _artifactRepo.AddAsync(artifact);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ExecuteAsync(feature.Id, artifact.Id));
    }

    [Fact]
    public async Task ExecuteAsync_WhenFeatureNotInRequiredStatus_ThrowsInvalidOperationException()
    {
        // Feature is in Ideation — cannot approve a PRD artifact (requires PrdPending)
        var feature = Feature.Create(Guid.NewGuid(), "prompt");
        await _featureRepo.AddAsync(feature);

        var artifact = Artifact.Create(feature.Id, ArtifactType.Prd);
        await _artifactRepo.AddAsync(artifact);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ExecuteAsync(feature.Id, artifact.Id));
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenWorkflowApproved_MarksArtifactAndAdvancesToDeployed()
    {
        // Workflow approval transitions QaApproved → Deployed.
        // Orchestrator receives Deployed which falls into the default/no-op branch.
        var feature = CreateFeatureAt(Guid.NewGuid(), FeatureStatus.QaApproved);
        await _featureRepo.AddAsync(feature);

        var artifact = Artifact.Create(feature.Id, ArtifactType.Workflow);
        await _artifactRepo.AddAsync(artifact);

        var result = await _sut.ExecuteAsync(feature.Id, artifact.Id);

        Assert.Equal("tl@test.com", result.ApprovedBy);
        Assert.NotNull(result.ApprovedAt);
        Assert.Equal(FeatureStatus.Deployed, feature.Status);
    }

    [Fact]
    public async Task ExecuteAsync_WhenWorkflowApproved_RecordsPipelineEvent()
    {
        var feature = CreateFeatureAt(Guid.NewGuid(), FeatureStatus.QaApproved);
        await _featureRepo.AddAsync(feature);

        var artifact = Artifact.Create(feature.Id, ArtifactType.Workflow);
        await _artifactRepo.AddAsync(artifact);

        await _sut.ExecuteAsync(feature.Id, artifact.Id);

        var events = _eventRepo.All;
        Assert.Single(events);
        Assert.Equal(FeatureStatus.QaApproved, events[0].FromStatus);
        Assert.Equal(FeatureStatus.Deployed, events[0].ToStatus);
        Assert.Equal("tl@test.com", events[0].TriggeredBy);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFeatureIsPastRequiredStatus_OnlyMarksApproved_DoesNotAdvance()
    {
        // Simulate a force-rerun scenario: artifact type is PRD but feature is already at Coding
        var feature = CreateFeatureAt(Guid.NewGuid(), FeatureStatus.Coding);
        await _featureRepo.AddAsync(feature);

        var artifact = Artifact.Create(feature.Id, ArtifactType.Prd);
        await _artifactRepo.AddAsync(artifact);

        var result = await _sut.ExecuteAsync(feature.Id, artifact.Id);

        // Artifact marked approved
        Assert.NotNull(result.ApprovedBy);
        // Feature status unchanged — not re-advanced
        Assert.Equal(FeatureStatus.Coding, feature.Status);
        // No pipeline event recorded
        Assert.Empty(_eventRepo.All);
    }
    [Fact]
    public async Task ExecuteAsync_WhenCallerIsNotAuthenticated_ThrowsUnauthorizedApprovalException()
    {
        var feature = CreateFeatureAt(Guid.NewGuid(), FeatureStatus.PrdPending);
        await _featureRepo.AddAsync(feature);

        var artifact = Artifact.Create(feature.Id, ArtifactType.Prd);
        await _artifactRepo.AddAsync(artifact);

        _userContext.IsAuthenticated = false;
        _userContext.UserId = null;

        await Assert.ThrowsAsync<UnauthorizedApprovalException>(() =>
            _sut.ExecuteAsync(feature.Id, artifact.Id));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCallerLacksTheRequiredRole_ThrowsUnauthorizedApprovalException()
    {
        // A PRD requires PM/EM; this caller only holds QA.
        var feature = CreateFeatureAt(Guid.NewGuid(), FeatureStatus.PrdPending);
        await _featureRepo.AddAsync(feature);

        var artifact = Artifact.Create(feature.Id, ArtifactType.Prd);
        await _artifactRepo.AddAsync(artifact);

        _userContext.Roles = new[] { TeamRole.Qa };

        await Assert.ThrowsAsync<UnauthorizedApprovalException>(() =>
            _sut.ExecuteAsync(feature.Id, artifact.Id));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCallerLacksTheRequiredRole_ShouldNotApproveTheArtifact()
    {
        var feature = CreateFeatureAt(Guid.NewGuid(), FeatureStatus.PrdPending);
        await _featureRepo.AddAsync(feature);

        var artifact = Artifact.Create(feature.Id, ArtifactType.Prd);
        await _artifactRepo.AddAsync(artifact);

        _userContext.Roles = new[] { TeamRole.Qa };

        await Assert.ThrowsAsync<UnauthorizedApprovalException>(() =>
            _sut.ExecuteAsync(feature.Id, artifact.Id));

        Assert.Null(artifact.ApprovedBy);
        Assert.Null(artifact.ApprovedAt);
        Assert.Equal(FeatureStatus.PrdPending, feature.Status);
    }

    [Fact]
    public async Task ExecuteAsync_WhenApproved_ShouldRecordTheAuthenticatedCallerAsApprover()
    {
        var feature = CreateFeatureAt(Guid.NewGuid(), FeatureStatus.PrdPending);
        await _featureRepo.AddAsync(feature);

        var artifact = Artifact.Create(feature.Id, ArtifactType.Prd);
        await _artifactRepo.AddAsync(artifact);

        _userContext.UserId = "someone@zephyrus.dev";

        var result = await _sut.ExecuteAsync(feature.Id, artifact.Id);

        Assert.Equal("someone@zephyrus.dev", result.ApprovedBy);
    }

}
