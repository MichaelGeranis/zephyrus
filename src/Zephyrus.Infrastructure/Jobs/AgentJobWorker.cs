using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;
using Zephyrus.Infrastructure.AI;
using Zephyrus.Infrastructure.Telemetry;

namespace Zephyrus.Infrastructure.Jobs;

/// <summary>
/// Runs queued agent jobs one at a time. Each job runs in its own DI scope,
/// because the request that queued it has already finished.
/// </summary>
/// <remarks>
/// The retry policy lives in <see cref="AgentJobRecord"/>: a failed job is
/// retried once, then marked failed. The feature keeps its current status
/// throughout, so a person can re-run the step.
/// </remarks>
public sealed class AgentJobWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AgentJobSignal _signal;
    private readonly AgentJobQueueOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<AgentJobWorker> _logger;

    public AgentJobWorker(
        IServiceScopeFactory scopeFactory,
        AgentJobSignal signal,
        IOptions<AgentJobQueueOptions> options,
        TimeProvider time,
        ILogger<AgentJobWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _signal = signal;
        _options = options.Value;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Agent job worker started.");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    while (await RunNextAsync(stoppingToken))
                    {
                        // Keep going until no job is due.
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // The database being briefly unreachable must not stop the worker.
                    _logger.LogError(ex, "Agent job worker could not read the job queue; retrying.");
                }

                await _signal.WaitAsync(_options.PollInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown. A job that was running keeps its lease and
            // is picked up again once the lease expires.
        }

        _logger.LogInformation("Agent job worker stopped.");
    }

    /// <returns><c>true</c> when a job was run, so the caller should look for another.</returns>
    public async Task<bool> RunNextAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IAgentJobRepository>();

        var record = await jobs.TryClaimNextAsync(Now(), _options.Lease, ct);
        if (record is null)
            return false;

        var job = record.ToJob();

        // Continue the trace of the request that queued this job.
        ActivityContext.TryParse(record.TraceParent, null, out var parent);
        using var activity = ZephyrusTelemetry.Source.StartActivity(
            $"agent job {job.Kind}", ActivityKind.Consumer, parent);
        activity?.SetTag("zephyrus.feature_id", job.FeatureId);
        activity?.SetTag("zephyrus.job.id", record.Id);
        activity?.SetTag("zephyrus.job.kind", job.Kind.ToString());
        activity?.SetTag("zephyrus.job.attempt", record.Attempts);

        // A retry goes to the fallback model rather than repeating the same
        // failure on the primary (see FallbackLanguageModel).
        var routing = scope.ServiceProvider.GetService<LanguageModelRouting>();
        if (routing is not null)
            routing.PreferFallback = record.Attempts > 1;

        try
        {
            await scope.ServiceProvider.GetRequiredService<IAgentJobDispatcher>().DispatchAsync(job, ct);
            record.Succeed(Now());

            _logger.LogInformation(
                "Feature {FeatureId}: {Kind} agent job completed (attempt {Attempt}).",
                job.FeatureId, job.Kind, record.Attempts);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            record.Fail(ex.Message, Now(), _options.RetryDelay);
            ZephyrusTelemetry.RecordError(activity, ex);

            _logger.LogError(
                ex,
                "Feature {FeatureId}: {Kind} agent job failed on attempt {Attempt} of {MaxAttempts}; now {Status}.",
                job.FeatureId, job.Kind, record.Attempts, AgentJobRecord.MaxAttempts, record.Status);
        }

        await jobs.UpdateAsync(record, ct);
        return true;
    }

    private DateTime Now() => _time.GetUtcNow().UtcDateTime;
}
