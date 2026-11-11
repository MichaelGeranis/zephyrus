using Zephyrus.Application.Exceptions;
using Zephyrus.Application.Orchestration;
using Zephyrus.Application.UseCases;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Jobs;
using Zephyrus.UnitTests.Jobs;

namespace Zephyrus.UnitTests.UseCases;

public class QueuePrdAgentUseCaseTests
{
    private readonly InMemoryFeatureRepository _features = new();
    private readonly InMemoryAgentJobRepository _jobs = new();
    private readonly RecordingJobQueue _queue = new();
    private readonly QueuePrdAgentUseCase _sut;

    public QueuePrdAgentUseCaseTests()
    {
        _sut = new QueuePrdAgentUseCase(_features, new AgentRunRequests(_queue, _jobs));
    }

    private async Task<Feature> FeatureAt(FeatureStatus status)
    {
        var feature = Feature.Create(Guid.NewGuid(), "Export invoices");
        while (feature.Status != status)
            feature.Advance();
        await _features.AddAsync(feature, Ct);
        return feature;
    }

    [Theory]
    [InlineData(FeatureStatus.Ideation)]
    [InlineData(FeatureStatus.PrdPending)]
    public async Task ExecuteAsync_WhenFeatureCanHaveAPrd_ShouldQueueThePrdAgent(FeatureStatus status)
    {
        var feature = await FeatureAt(status);

        await _sut.ExecuteAsync(feature.Id, Ct);

        Assert.Equal(new AgentJob(feature.Id, AgentJobKind.Prd), Assert.Single(_queue.Enqueued));
    }

    [Fact]
    public async Task ExecuteAsync_WhenFeatureIsPastThePrd_ShouldRejectAndQueueNothing()
    {
        var feature = await FeatureAt(FeatureStatus.ArchPending);

        await Assert.ThrowsAsync<RequestRejectedException>(() => _sut.ExecuteAsync(feature.Id, Ct));
        Assert.Empty(_queue.Enqueued);
    }

    [Fact]
    public async Task ExecuteAsync_WhenFeatureDoesNotExist_ShouldThrowResourceNotFoundException()
    {
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => _sut.ExecuteAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task ExecuteAsync_WhenThePrdAgentIsAlreadyQueued_ShouldReject()
    {
        var feature = await FeatureAt(FeatureStatus.Ideation);
        await _jobs.AddAsync(AgentJobRecord.Create(new AgentJob(feature.Id, AgentJobKind.Prd), DateTime.UtcNow), Ct);

        await Assert.ThrowsAsync<RequestRejectedException>(() => _sut.ExecuteAsync(feature.Id, Ct));
        Assert.Empty(_queue.Enqueued);
    }
}
