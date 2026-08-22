using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Zephyrus.Core.Interfaces;
using Zephyrus.Core.Jobs;

namespace Zephyrus.Infrastructure.Jobs;

/// <summary>
/// Drains <see cref="BackgroundJobQueue"/> and runs each job in its own DI
/// scope, so agent work no longer holds the request that triggered it.
/// </summary>
public sealed class AgentJobWorker : BackgroundService
{
    private readonly BackgroundJobQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AgentJobWorker> _logger;

    public AgentJobWorker(
        BackgroundJobQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<AgentJobWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Agent job worker started.");

        try
        {
            await foreach (var job in _queue.ReadAllAsync(stoppingToken))
            {
                await RunAsync(job, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }

        _logger.LogInformation("Agent job worker stopped.");
    }

    /// <summary>
    /// Runs one job. A failure is logged and swallowed: the feature keeps its
    /// current status and stays recoverable via rerun-step, and one bad job
    /// must never take the worker down with it.
    /// </summary>
    private async Task RunAsync(AgentJob job, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        try
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IAgentJobDispatcher>();
            await dispatcher.DispatchAsync(job, ct);

            _logger.LogInformation(
                "Feature {FeatureId}: {Kind} agent job completed.", job.FeatureId, job.Kind);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Feature {FeatureId}: {Kind} agent job failed. The feature keeps its current status; re-run the step to retry.",
                job.FeatureId,
                job.Kind);
        }
    }
}
