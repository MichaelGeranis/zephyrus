using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zephyrus.Application.UseCases;
using Zephyrus.Core.Entities;
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
            "Feature {FeatureId}: dispatching {Kind} agent job (re-run: {Rerun}).", job.FeatureId, job.Kind, job.ForceRerun);

        await SelectProjectModelsAsync(job.FeatureId, ct);

        var rerun = job.ForceRerun;

        switch (job.Kind)
        {
            case AgentJobKind.Prd:
                await GetRequiredService<InvokePrdAgentUseCase>().ExecuteAsync(job.FeatureId, rerun, ct);
                break;

            case AgentJobKind.Architect:
                await GetRequiredService<InvokeArchitectAgentUseCase>().ExecuteAsync(job.FeatureId, rerun, ct);
                break;

            case AgentJobKind.Task:
                await GetRequiredService<InvokeTaskAgentUseCase>().ExecuteAsync(job.FeatureId, rerun, ct);
                break;

            case AgentJobKind.Code:
                await GetRequiredService<InvokeCodeAgentUseCase>().ExecuteAsync(job.FeatureId, rerun, ct);
                break;

            case AgentJobKind.Qa:
                await GetRequiredService<InvokeQaAgentUseCase>().ExecuteAsync(job.FeatureId, rerun, ct);
                break;

            case AgentJobKind.DevOps:
                await GetRequiredService<InvokeDevOpsAgentUseCase>().ExecuteAsync(job.FeatureId, rerun, ct);
                break;

            default:
                throw new InvalidOperationException($"Unknown agent job kind '{job.Kind}'.");
        }
    }

    /// <summary>
    /// Every agent run comes through here, so this is where the feature's
    /// project chooses the models its agents use.
    /// </summary>
    private async Task SelectProjectModelsAsync(Guid featureId, CancellationToken ct)
    {
        var feature = await GetRequiredService<IFeatureRepository>().GetByIdAsync(featureId, ct);
        if (feature is null)
            return; // the use case reports the missing feature

        var changes = await GetRequiredService<IModelChoiceRepository>().GetByProjectIdAsync(feature.ProjectId, ct);
        GetRequiredService<IProjectModelSelection>().Select(ModelChoices.Current(changes));
    }

    private T GetRequiredService<T>() where T : notnull
        => _serviceProvider.GetRequiredService<T>();
}
