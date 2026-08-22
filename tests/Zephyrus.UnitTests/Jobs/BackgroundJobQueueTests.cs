using Zephyrus.Core.Jobs;
using Zephyrus.Infrastructure.Jobs;

namespace Zephyrus.UnitTests.Jobs;

public class BackgroundJobQueueTests
{
    [Fact]
    public async Task EnqueueAsync_WhenJobQueued_ShouldBeReadableByConsumer()
    {
        var sut = new BackgroundJobQueue();
        var job = new AgentJob(Guid.NewGuid(), AgentJobKind.Architect);

        await sut.EnqueueAsync(job);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var read in sut.ReadAllAsync(cts.Token))
        {
            Assert.Equal(job, read);
            break;
        }
    }

    [Fact]
    public async Task ReadAllAsync_WhenMultipleJobsQueued_ShouldPreserveFifoOrder()
    {
        var sut = new BackgroundJobQueue();
        var featureId = Guid.NewGuid();
        var expected = new[]
        {
            new AgentJob(featureId, AgentJobKind.Architect),
            new AgentJob(featureId, AgentJobKind.Task),
            new AgentJob(featureId, AgentJobKind.Code),
        };

        foreach (var job in expected)
            await sut.EnqueueAsync(job);

        var actual = new List<AgentJob>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await foreach (var read in sut.ReadAllAsync(cts.Token))
        {
            actual.Add(read);
            if (actual.Count == expected.Length)
                break;
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task EnqueueAsync_WhenCalled_ShouldReturnWithoutWaitingForAConsumer()
    {
        // An unbounded queue must never block the caller — this is what keeps the
        // approval request fast.
        var sut = new BackgroundJobQueue();

        var enqueue = sut.EnqueueAsync(new AgentJob(Guid.NewGuid(), AgentJobKind.Qa)).AsTask();

        Assert.True(enqueue.IsCompleted);
        await enqueue;
    }
}
