using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zephyrus.Application.UseCases;
using Zephyrus.Core.Interfaces;
using Zephyrus.Core.Jobs;

namespace Zephyrus.Application.Orchestration;

/// <summary>
/// Routes a queued <see cref="AgentJob"/> to the use case that runs the agent.
/// Deterministic — it follows the trigger map exactly and makes no decisions.
/// </summary>
public sealed class AgentJobDispatcher : IAgentJobDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AgentJobDispatcher> _logger;

    public AgentJobDispatcher(
        IServiceProvider serviceProvider,
        ILogger<AgentJobDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task DispatchAsync(AgentJob job, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Feature {FeatureId}: dispatching {Kind} agent job.", job.FeatureId, job.Kind);

        switch (job.Kind)
        {
            case AgentJobKind.Architect:
                await GetRequiredService<InvokeArchitectAgentUseCase>().ExecuteAsync(job.FeatureId, ct: ct);
                break;

            case AgentJobKind.Task:
                await GetRequiredService<InvokeTaskAgentUseCase>().ExecuteAsync(job.FeatureId, ct: ct);
                break;

            case AgentJobKind.Code:
                await GetRequiredService<InvokeCodeAgentUseCase>().ExecuteAsync(job.FeatureId, ct: ct);
                break;

            case AgentJobKind.Qa:
                await GetRequiredService<InvokeQaAgentUseCase>().ExecuteAsync(job.FeatureId, ct: ct);
                break;

            case AgentJobKind.DevOps:
                await GetRequiredService<InvokeDevOpsAgentUseCase>().ExecuteAsync(job.FeatureId, ct: ct);
                break;

            default:
                throw new InvalidOperationException($"Unknown agent job kind '{job.Kind}'.");
        }
    }

    private T GetRequiredService<T>() where T : notnull
        => _serviceProvider.GetRequiredService<T>();
}
