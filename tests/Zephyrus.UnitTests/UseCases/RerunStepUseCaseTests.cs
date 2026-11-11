using Zephyrus.Application.Exceptions;
using Zephyrus.Application.Orchestration;
using Zephyrus.Application.UseCases;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Jobs;
using Zephyrus.UnitTests.Jobs;

namespace Zephyrus.UnitTests.UseCases;

public class RerunStepUseCaseTests
{
    private readonly InMemoryFeatureRepository _featureRepo = new();
    private readonly InMemoryAgentJobRepository _jobs = new();
    private readonly RecordingJobQueue _queue = new();
    private readonly RerunStepUseCase _sut;

    public RerunStepUseCaseTests()
    {
        _sut = new RerunStepUseCase(_featureRepo, new AgentRunRequests(_queue, _jobs));
    }

    [Fact]
    public async Task ExecuteAsync_WhenFeatureNotFound_ThrowsResourceNotFoundException()
    {
        await Assert.ThrowsAsync<ResourceNotFoundException>(() =>
            _sut.ExecuteAsync(Guid.NewGuid(), ct: Ct));
    }

    private static Feature CreateFeatureAt(Guid projectId, FeatureStatus targetStatus)
    {
        var feature = Feature.Create(projectId, "prompt");
        while (feature.Status != targetStatus)
            feature.Advance();
        return feature;
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnknownStep_ThrowsRequestRejectedException()
    {
        var feature = CreateFeatureAt(Guid.NewGuid(), FeatureStatus.PrdPending);
        await _featureRepo.AddAsync(feature, Ct);

        await Assert.ThrowsAsync<RequestRejectedException>(() =>
            _sut.ExecuteAsync(feature.Id, "unknownstep", Ct));
    }

    [Theory]
    [InlineData(FeatureStatus.Ideation)]
    [InlineData(FeatureStatus.PrdApproved)]
    [InlineData(FeatureStatus.ArchApproved)]
    [InlineData(FeatureStatus.TasksApproved)]
    [InlineData(FeatureStatus.Deployed)]
    public async Task ExecuteAsync_WhenStatusHasNoRerunStep_ThrowsRequestRejectedException(FeatureStatus status)
    {
        var feature = CreateFeatureAt(Guid.NewGuid(), status);
        await _featureRepo.AddAsync(feature, Ct);

        await Assert.ThrowsAsync<RequestRejectedException>(() =>
            _sut.ExecuteAsync(feature.Id, ct: Ct));
    }

    [Theory]
    [InlineData(FeatureStatus.PrdPending, AgentJobKind.Prd)]
    [InlineData(FeatureStatus.ArchPending, AgentJobKind.Architect)]
    [InlineData(FeatureStatus.TasksPending, AgentJobKind.Task)]
    [InlineData(FeatureStatus.Coding, AgentJobKind.Code)]
    [InlineData(FeatureStatus.QaPending, AgentJobKind.Qa)]
    [InlineData(FeatureStatus.QaApproved, AgentJobKind.DevOps)]
    public async Task ExecuteAsync_WhenNoStepIsNamed_ShouldQueueTheStepForTheCurrentStatus(FeatureStatus status, AgentJobKind expected)
    {
        var feature = CreateFeatureAt(Guid.NewGuid(), status);
        await _featureRepo.AddAsync(feature, Ct);

        await _sut.ExecuteAsync(feature.Id, ct: Ct);

        Assert.Equal(new AgentJob(feature.Id, expected, ForceRerun: false), Assert.Single(_queue.Enqueued));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAStepIsNamed_ShouldQueueItAsAForcedRerun()
    {
        var feature = CreateFeatureAt(Guid.NewGuid(), FeatureStatus.Coding);
        await _featureRepo.AddAsync(feature, Ct);

        await _sut.ExecuteAsync(feature.Id, "ARCHITECT", Ct);

        Assert.Equal(new AgentJob(feature.Id, AgentJobKind.Architect, ForceRerun: true), Assert.Single(_queue.Enqueued));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnAgentJobIsStillActive_ShouldRejectAndQueueNothing()
    {
        var feature = CreateFeatureAt(Guid.NewGuid(), FeatureStatus.ArchPending);
        await _featureRepo.AddAsync(feature, Ct);
        await _jobs.AddAsync(AgentJobRecord.Create(new AgentJob(feature.Id, AgentJobKind.Architect), DateTime.UtcNow), Ct);

        var error = await Assert.ThrowsAsync<RequestRejectedException>(() => _sut.ExecuteAsync(feature.Id, ct: Ct));

        Assert.Contains("already queued", error.Message);
        Assert.Empty(_queue.Enqueued);
    }
}
