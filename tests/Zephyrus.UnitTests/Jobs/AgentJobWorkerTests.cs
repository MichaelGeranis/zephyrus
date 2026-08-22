using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Zephyrus.Core.Interfaces;
using Zephyrus.Core.Jobs;
using Zephyrus.Infrastructure.Jobs;

namespace Zephyrus.UnitTests.Jobs;

public class AgentJobWorkerTests
{
    private static (AgentJobWorker Worker, BackgroundJobQueue Queue) CreateWorker(
        RecordingJobDispatcher dispatcher)
    {
        var services = new ServiceCollection();
        services.AddScoped<IAgentJobDispatcher>(_ => dispatcher);
        var provider = services.BuildServiceProvider();

        var queue = new BackgroundJobQueue();
        var worker = new AgentJobWorker(
            queue,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AgentJobWorker>.Instance);

        return (worker, queue);
    }

    private static async Task WaitForAsync(Task task)
    {
        var finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.True(
            ReferenceEquals(finished, task),
            "Timed out waiting for the worker to dispatch queued jobs.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenJobQueued_ShouldDispatchIt()
    {
        var dispatcher = new RecordingJobDispatcher(expectedCount: 1);
        var (worker, queue) = CreateWorker(dispatcher);
        var job = new AgentJob(Guid.NewGuid(), AgentJobKind.Architect);

        await worker.StartAsync(CancellationToken.None);
        await queue.EnqueueAsync(job);
        await WaitForAsync(dispatcher.AllDispatched);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(job, Assert.Single(dispatcher.Dispatched));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAJobThrows_ShouldKeepProcessingLaterJobs()
    {
        // A failing agent must not take the worker down — the feature stays in its
        // current status and is recovered with rerun-step.
        var dispatcher = new RecordingJobDispatcher(expectedCount: 2);
        dispatcher.FailOn.Add(AgentJobKind.Architect);
        var (worker, queue) = CreateWorker(dispatcher);
        var featureId = Guid.NewGuid();

        await worker.StartAsync(CancellationToken.None);
        await queue.EnqueueAsync(new AgentJob(featureId, AgentJobKind.Architect));
        await queue.EnqueueAsync(new AgentJob(featureId, AgentJobKind.Task));
        await WaitForAsync(dispatcher.AllDispatched);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(2, dispatcher.Dispatched.Count);
        Assert.Contains(dispatcher.Dispatched, j => j.Kind == AgentJobKind.Task);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMultipleJobsQueued_ShouldDispatchAllOfThem()
    {
        var dispatcher = new RecordingJobDispatcher(expectedCount: 3);
        var (worker, queue) = CreateWorker(dispatcher);
        var featureId = Guid.NewGuid();

        await worker.StartAsync(CancellationToken.None);
        await queue.EnqueueAsync(new AgentJob(featureId, AgentJobKind.Architect));
        await queue.EnqueueAsync(new AgentJob(featureId, AgentJobKind.Task));
        await queue.EnqueueAsync(new AgentJob(featureId, AgentJobKind.Code));
        await WaitForAsync(dispatcher.AllDispatched);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(3, dispatcher.Dispatched.Count);
    }
}
