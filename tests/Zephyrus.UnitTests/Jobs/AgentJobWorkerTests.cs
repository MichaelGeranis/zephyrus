using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Interfaces;
using Zephyrus.Core.Jobs;
using Zephyrus.Infrastructure.AI;
using Zephyrus.Infrastructure.Jobs;

namespace Zephyrus.UnitTests.Jobs;

public class AgentJobWorkerTests
{
    private readonly InMemoryAgentJobRepository _jobs = new();
    private readonly ManualTimeProvider _time = new(new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc));
    private readonly AgentJobQueueOptions _options = new()
    {
        PollInterval = TimeSpan.FromMilliseconds(20),
        Lease = TimeSpan.FromMinutes(30),
        RetryDelay = TimeSpan.FromSeconds(30),
    };

    private AgentJobWorker CreateWorker(RecordingJobDispatcher dispatcher)
    {
        var services = new ServiceCollection();
        services.AddScoped<IAgentJobDispatcher>(_ => dispatcher);
        services.AddScoped<IAgentJobRepository>(_ => _jobs);
        var provider = services.BuildServiceProvider();

        return new AgentJobWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new AgentJobSignal(),
            Options.Create(_options),
            _time,
            NullLogger<AgentJobWorker>.Instance);
    }

    private async Task<AgentJobRecord> Enqueue(AgentJobKind kind, Guid? featureId = null)
    {
        var record = AgentJobRecord.Create(new AgentJob(featureId ?? Guid.NewGuid(), kind), _time.Now);
        await _jobs.AddAsync(record);
        return record;
    }

    [Fact]
    public async Task RunNextAsync_WhenJobQueued_ShouldDispatchItAndMarkItSucceeded()
    {
        var dispatcher = new RecordingJobDispatcher();
        var record = await Enqueue(AgentJobKind.Architect);

        var ran = await CreateWorker(dispatcher).RunNextAsync(CancellationToken.None);

        Assert.True(ran);
        Assert.Equal(record.ToJob(), Assert.Single(dispatcher.Dispatched));
        Assert.Equal(AgentJobStatus.Succeeded, record.Status);
    }

    [Fact]
    public async Task RunNextAsync_WhenNoJobIsDue_ShouldReturnFalse()
    {
        Assert.False(await CreateWorker(new RecordingJobDispatcher()).RunNextAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RunNextAsync_WhenJobFailsOnce_ShouldRetryItAfterTheDelayAndThenSucceed()
    {
        var dispatcher = new RecordingJobDispatcher(expectedCount: 2);
        dispatcher.FailOn.Add(AgentJobKind.Code);
        var worker = CreateWorker(dispatcher);
        var record = await Enqueue(AgentJobKind.Code);

        await worker.RunNextAsync(CancellationToken.None);
        Assert.Equal(AgentJobStatus.Queued, record.Status);
        Assert.Contains("Simulated failure", record.LastError);

        // Not due yet.
        Assert.False(await worker.RunNextAsync(CancellationToken.None));

        _time.Advance(_options.RetryDelay);
        dispatcher.FailOn.Clear();
        await worker.RunNextAsync(CancellationToken.None);

        Assert.Equal(AgentJobStatus.Succeeded, record.Status);
        Assert.Equal(2, record.Attempts);
    }

    [Fact]
    public async Task RunNextAsync_WhenRetryAlsoFails_ShouldMarkFailedAndStop()
    {
        var dispatcher = new RecordingJobDispatcher(expectedCount: 2);
        dispatcher.FailOn.Add(AgentJobKind.Qa);
        var worker = CreateWorker(dispatcher);
        var record = await Enqueue(AgentJobKind.Qa);

        await worker.RunNextAsync(CancellationToken.None);
        _time.Advance(_options.RetryDelay);
        await worker.RunNextAsync(CancellationToken.None);
        _time.Advance(TimeSpan.FromHours(1));

        Assert.Equal(AgentJobStatus.Failed, record.Status);
        Assert.False(await worker.RunNextAsync(CancellationToken.None));
        Assert.Equal(2, dispatcher.Dispatched.Count);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAJobThrows_ShouldKeepProcessingLaterJobs()
    {
        // A failing agent must not take the worker down.
        var dispatcher = new RecordingJobDispatcher(expectedCount: 2);
        dispatcher.FailOn.Add(AgentJobKind.Architect);
        var worker = CreateWorker(dispatcher);
        var featureId = Guid.NewGuid();
        await Enqueue(AgentJobKind.Architect, featureId);
        await Enqueue(AgentJobKind.Task, featureId);

        await worker.StartAsync(CancellationToken.None);
        await WaitForAsync(dispatcher.AllDispatched);
        await worker.StopAsync(CancellationToken.None);

        Assert.Contains(dispatcher.Dispatched, j => j.Kind == AgentJobKind.Task);
    }

    [Fact]
    public async Task ExecuteAsync_WhenJobsAreQueued_ShouldDispatchThemInOrder()
    {
        var dispatcher = new RecordingJobDispatcher(expectedCount: 3);
        var worker = CreateWorker(dispatcher);
        var featureId = Guid.NewGuid();
        await Enqueue(AgentJobKind.Architect, featureId);
        _time.Advance(TimeSpan.FromSeconds(1));
        await Enqueue(AgentJobKind.Task, featureId);
        _time.Advance(TimeSpan.FromSeconds(1));
        await Enqueue(AgentJobKind.Code, featureId);

        await worker.StartAsync(CancellationToken.None);
        await WaitForAsync(dispatcher.AllDispatched);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(
            new[] { AgentJobKind.Architect, AgentJobKind.Task, AgentJobKind.Code },
            dispatcher.Dispatched.Select(j => j.Kind));
    }

    [Fact]
    public async Task RunNextAsync_WhenRetryingAFailedJob_ShouldAskForTheFallbackModel()
    {
        // The first attempt failed on the primary model (often malformed output),
        // so the retry goes to the fallback instead of repeating it.
        var seen = new List<bool>();
        var dispatcher = new RecordingJobDispatcher(expectedCount: 2);
        dispatcher.FailOn.Add(AgentJobKind.Task);

        var services = new ServiceCollection();
        services.AddScoped<LanguageModelRouting>();
        services.AddScoped<IAgentJobRepository>(_ => _jobs);
        services.AddScoped<IAgentJobDispatcher>(sp => new RoutingProbe(dispatcher, sp.GetRequiredService<LanguageModelRouting>(), seen));
        var worker = new AgentJobWorker(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            new AgentJobSignal(), Options.Create(_options), _time, NullLogger<AgentJobWorker>.Instance);
        await Enqueue(AgentJobKind.Task);

        await worker.RunNextAsync(CancellationToken.None);
        _time.Advance(_options.RetryDelay);
        await worker.RunNextAsync(CancellationToken.None);

        Assert.Equal(new[] { false, true }, seen);
    }

    private sealed class RoutingProbe : IAgentJobDispatcher
    {
        private readonly IAgentJobDispatcher _inner;
        private readonly LanguageModelRouting _routing;
        private readonly List<bool> _seen;

        public RoutingProbe(IAgentJobDispatcher inner, LanguageModelRouting routing, List<bool> seen)
        {
            _inner = inner;
            _routing = routing;
            _seen = seen;
        }

        public Task DispatchAsync(AgentJob job, CancellationToken ct = default)
        {
            _seen.Add(_routing.PreferFallback);
            return _inner.DispatchAsync(job, ct);
        }
    }

    private static async Task WaitForAsync(Task task)
    {
        var finished = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.True(ReferenceEquals(finished, task), "Timed out waiting for the worker to dispatch queued jobs.");
    }
}
