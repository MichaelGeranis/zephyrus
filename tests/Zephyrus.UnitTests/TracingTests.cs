using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;
using Zephyrus.Core.Jobs;
using Zephyrus.Infrastructure.AI;
using Zephyrus.Infrastructure.Jobs;
using Zephyrus.Infrastructure.Telemetry;
using Zephyrus.UnitTests.AI;
using Zephyrus.UnitTests.Jobs;

namespace Zephyrus.UnitTests;

/// <summary>
/// A request, the job it queues and the model calls that job makes must end up
/// in one trace.
/// </summary>
public sealed class TracingTests : IDisposable
{
    // A constant, not TestSource.Name: creating an ActivitySource calls every
    // registered listener's filter, including while TestSource is still null.
    private const string TestSourceName = "Zephyrus.Tests.Tracing";
    private static readonly ActivitySource TestSource = new(TestSourceName);

    private readonly ConcurrentBag<Activity> _finished = new();
    private readonly ActivityListener _listener;

    public TracingTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name is ZephyrusTelemetry.SourceName or TestSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a => _finished.Add(a),
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public async Task EnqueueAsync_WhenCalledInsideATrace_ShouldStoreTheTraceParent()
    {
        var jobs = new InMemoryAgentJobRepository();
        var queue = new DurableJobQueue(jobs, new AgentJobSignal(), TimeProvider.System);
        var featureId = Guid.NewGuid();

        using (var request = TestSource.StartActivity("POST approve"))
        {
            await queue.EnqueueAsync(new AgentJob(featureId, AgentJobKind.Architect), Ct);

            var stored = Assert.Single(await jobs.GetByFeatureIdAsync(featureId, Ct));
            Assert.Equal(request!.Id, stored.TraceParent);
        }
    }

    [Fact]
    public async Task RunNextAsync_WhenJobCarriesATraceParent_ShouldContinueThatTrace()
    {
        using var request = TestSource.StartActivity("POST approve")!;
        var traceParent = request.Id;
        request.Stop();

        var jobs = new InMemoryAgentJobRepository();
        await jobs.AddAsync(AgentJobRecord.Create(new AgentJob(Guid.NewGuid(), AgentJobKind.Task), DateTime.UtcNow, traceParent), Ct);

        var services = new ServiceCollection();
        services.AddScoped<IAgentJobRepository>(_ => jobs);
        services.AddScoped<IAgentJobDispatcher>(_ => new RecordingJobDispatcher());
        var worker = new AgentJobWorker(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            new AgentJobSignal(),
            Options.Create(new AgentJobQueueOptions()),
            TimeProvider.System,
            NullLogger<AgentJobWorker>.Instance);

        await worker.RunNextAsync(CancellationToken.None);

        // Other tests run in parallel and also create spans; match this trace only.
        var jobSpan = Assert.Single(_finished, a => a.OperationName == "agent job Task" && a.TraceId == request.TraceId);
        Assert.Equal(request.TraceId, jobSpan.TraceId);
        Assert.Equal(request.SpanId, jobSpan.ParentSpanId);
        Assert.Equal("Task", jobSpan.GetTagItem("zephyrus.job.kind"));
    }

    [Fact]
    public async Task ModelCall_WhenItSucceeds_ShouldRecordTheModelAndTokens()
    {
        var handler = new StubHttpHandler("""
            {"model":"llama3.1:8b","choices":[{"message":{"content":"ok"},"finish_reason":"stop"}],
             "usage":{"prompt_tokens":42,"completion_tokens":7}}
            """);
        var model = new OpenAiCompatibleLanguageModel(
            new HttpClient(handler), Options.Create(new LocalModelOptions { Model = "tracing-ok-model" }), LanguageModelTestSupport.Tracker());

        await model.GenerateAsync("s", "u", Ct);

        // A model name no other test uses, since parallel tests create spans too.
        var span = Assert.Single(_finished, a => a.OperationName == "chat tracing-ok-model");
        Assert.Equal(ActivityKind.Client, span.Kind);
        Assert.Equal("openai", span.GetTagItem("gen_ai.system"));
        Assert.Equal(42, span.GetTagItem("gen_ai.usage.input_tokens"));
        Assert.Equal(7, span.GetTagItem("gen_ai.usage.output_tokens"));
    }

    [Fact]
    public async Task ModelCall_WhenItFails_ShouldMarkTheSpanAsAnError()
    {
        var handler = new StubHttpHandler("""{"error":"boom"}""", HttpStatusCode.InternalServerError);
        var model = new OpenAiCompatibleLanguageModel(
            new HttpClient(handler), Options.Create(new LocalModelOptions { Model = "broken-model" }), LanguageModelTestSupport.Tracker());

        await Assert.ThrowsAsync<HttpRequestException>(() => model.GenerateAsync("s", "u", Ct));

        var span = Assert.Single(_finished, a => a.OperationName == "chat broken-model");
        Assert.Equal(ActivityStatusCode.Error, span.Status);
    }
}
