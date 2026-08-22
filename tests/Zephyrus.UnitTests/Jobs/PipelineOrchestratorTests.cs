using Microsoft.Extensions.Logging.Abstractions;
using Zephyrus.Application.Orchestration;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Jobs;

namespace Zephyrus.UnitTests.Jobs;

public class PipelineOrchestratorTests
{
    private readonly RecordingJobQueue _queue = new();
    private readonly PipelineOrchestrator _sut;

    public PipelineOrchestratorTests()
    {
        _sut = new PipelineOrchestrator(_queue, NullLogger<PipelineOrchestrator>.Instance);
    }

    [Theory]
    [InlineData(FeatureStatus.PrdApproved, AgentJobKind.Architect)]
    [InlineData(FeatureStatus.ArchApproved, AgentJobKind.Task)]
    [InlineData(FeatureStatus.TasksApproved, AgentJobKind.Code)]
    [InlineData(FeatureStatus.QaPending, AgentJobKind.Qa)]
    [InlineData(FeatureStatus.QaApproved, AgentJobKind.DevOps)]
    public async Task OnArtifactApprovedAsync_WhenStatusTriggersAgent_ShouldEnqueueMatchingJob(
        FeatureStatus status, AgentJobKind expectedKind)
    {
        var featureId = Guid.NewGuid();

        await _sut.OnArtifactApprovedAsync(featureId, status);

        var job = Assert.Single(_queue.Enqueued);
        Assert.Equal(featureId, job.FeatureId);
        Assert.Equal(expectedKind, job.Kind);
    }

    [Theory]
    [InlineData(FeatureStatus.Ideation)]
    [InlineData(FeatureStatus.PrdPending)]
    [InlineData(FeatureStatus.ArchPending)]
    [InlineData(FeatureStatus.TasksPending)]
    [InlineData(FeatureStatus.Coding)]
    [InlineData(FeatureStatus.Deployed)]
    public async Task OnArtifactApprovedAsync_WhenStatusHasNoFollowUpAgent_ShouldEnqueueNothing(
        FeatureStatus status)
    {
        await _sut.OnArtifactApprovedAsync(Guid.NewGuid(), status);

        Assert.Empty(_queue.Enqueued);
    }

    [Fact]
    public async Task OnArtifactApprovedAsync_WhenCalled_ShouldNotRunTheAgentInline()
    {
        // The orchestrator must only queue work — running an agent inline is the
        // blocking behaviour the queue exists to remove.
        await _sut.OnArtifactApprovedAsync(Guid.NewGuid(), FeatureStatus.TasksApproved);

        Assert.Single(_queue.Enqueued);
    }
}
