using Microsoft.Extensions.Logging.Abstractions;
using Zephyrus.Application.Orchestration;
using Zephyrus.Application.UseCases;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Exceptions;
using Zephyrus.UnitTests.Jobs;

namespace Zephyrus.UnitTests.UseCases;

public class ApproveArtifactUseCaseTests
{
    private readonly InMemoryFeatureRepository _featureRepo = new();
    private readonly InMemoryArtifactRepository _artifactRepo = new();
    private readonly InMemoryPipelineEventRepository _eventRepo = new();
    private readonly ApproveArtifactUseCase _sut;

    public ApproveArtifactUseCaseTests()
    {
        // The orchestrator only queues follow-up agent work, so a recording queue
        // is enough here — agents are exercised by the integration tests.
        var orchestrator = new PipelineOrchestrator(
            new RecordingJobQueue(), NullLogger<PipelineOrchestrator>.Instance);

        _sut = new ApproveArtifactUseCase(_featureRepo, _artifactRepo, _eventRepo, orchestrator);
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
            _sut.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), "user"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenArtifactNotFound_ThrowsArtifactNotFoundException()
    {
        var feature = Feature.Create(Guid.NewGuid(), "prompt");
        await _featureRepo.AddAsync(feature);

        await Assert.ThrowsAsync<ArtifactNotFoundException>(() =>
            _sut.ExecuteAsync(feature.Id, Guid.NewGuid(), "user"));
    }

    [Fact]
    public async Task ExecuteAsync_WhenArtifactBelongsToDifferentFeature_ThrowsInvalidOperationException()
    {
        var feature = Feature.Create(Guid.NewGuid(), "prompt");
        await _featureRepo.AddAsync(feature);

        var artifact = Artifact.Create(Guid.NewGuid(), ArtifactType.Prd);
        await _artifactRepo.AddAsync(artifact);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.ExecuteAsync(feature.Id, artifact.Id, "user"));
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
            _sut.ExecuteAsync(feature.Id, artifact.Id, "second-approver"));
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
            _sut.ExecuteAsync(feature.Id, artifact.Id, "user"));
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenWorkflowApproved_MarksArtifactApproved()
    {
        var feature = CreateFeatureAt(Guid.NewGuid(), FeatureStatus.QaApproved);
        await _featureRepo.AddAsync(feature);

        var artifact = Artifact.Create(feature.Id, ArtifactType.Workflow);
        await _artifactRepo.AddAsync(artifact);

        var result = await _sut.ExecuteAsync(feature.Id, artifact.Id, "tl@test.com");

        Assert.Equal("tl@test.com", result.ApprovedBy);
        Assert.NotNull(result.ApprovedAt);
    }

    [Fact]
    public async Task ExecuteAsync_WhenWorkflowApproved_ShouldNotAdvanceToDeployed()
    {
        // Approving the generated workflow reviews the CI/CD config. Nothing has
        // shipped, so the feature must stay at QaApproved until a deployment
        // actually succeeds.
        var feature = CreateFeatureAt(Guid.NewGuid(), FeatureStatus.QaApproved);
        await _featureRepo.AddAsync(feature);

        var artifact = Artifact.Create(feature.Id, ArtifactType.Workflow);
        await _artifactRepo.AddAsync(artifact);

        await _sut.ExecuteAsync(feature.Id, artifact.Id, "tl@test.com");

        Assert.Equal(FeatureStatus.QaApproved, feature.Status);
    }

    [Fact]
    public async Task ExecuteAsync_WhenWorkflowApproved_ShouldNotRecordAPipelineEvent()
    {
        var feature = CreateFeatureAt(Guid.NewGuid(), FeatureStatus.QaApproved);
        await _featureRepo.AddAsync(feature);

        var artifact = Artifact.Create(feature.Id, ArtifactType.Workflow);
        await _artifactRepo.AddAsync(artifact);

        await _sut.ExecuteAsync(feature.Id, artifact.Id, "tl@test.com");

        Assert.Empty(_eventRepo.All);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFeatureIsPastRequiredStatus_OnlyMarksApproved_DoesNotAdvance()
    {
        // Simulate a force-rerun scenario: artifact type is PRD but feature is already at Coding
        var feature = CreateFeatureAt(Guid.NewGuid(), FeatureStatus.Coding);
        await _featureRepo.AddAsync(feature);

        var artifact = Artifact.Create(feature.Id, ArtifactType.Prd);
        await _artifactRepo.AddAsync(artifact);

        var result = await _sut.ExecuteAsync(feature.Id, artifact.Id, "user");

        // Artifact marked approved
        Assert.NotNull(result.ApprovedBy);
        // Feature status unchanged — not re-advanced
        Assert.Equal(FeatureStatus.Coding, feature.Status);
        // No pipeline event recorded
        Assert.Empty(_eventRepo.All);
    }
}
