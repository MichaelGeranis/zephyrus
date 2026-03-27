using Zephyrus.Core.Enums;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.UseCases;

/// <summary>
/// Re-runs the current pipeline step for a feature that is stuck in a
/// pending/in-progress status due to a partial failure.
/// </summary>
public sealed class RerunStepUseCase
{
    private readonly IFeatureRepository _featureRepository;
    private readonly IServiceProvider _serviceProvider;

    public RerunStepUseCase(
        IFeatureRepository featureRepository,
        IServiceProvider serviceProvider)
    {
        _featureRepository = featureRepository;
        _serviceProvider = serviceProvider;
    }

    public async Task ExecuteAsync(Guid featureId, string? step = null, CancellationToken ct = default)
    {
        var feature = await _featureRepository.GetByIdAsync(featureId, ct)
            ?? throw new InvalidOperationException($"Feature '{featureId}' not found.");

        if (step is not null)
        {
            switch (step.ToLowerInvariant())
            {
                case "prd":
                    await GetService<InvokePrdAgentUseCase>().ExecuteAsync(featureId, forceRerun: true, ct);
                    break;
                case "architect":
                    await GetService<InvokeArchitectAgentUseCase>().ExecuteAsync(featureId, forceRerun: true, ct);
                    break;
                case "tasks":
                    await GetService<InvokeTaskAgentUseCase>().ExecuteAsync(featureId, forceRerun: true, ct);
                    break;
                case "code":
                    await GetService<InvokeCodeAgentUseCase>().ExecuteAsync(featureId, forceRerun: true, ct);
                    break;
                case "qa":
                    await GetService<InvokeQaAgentUseCase>().ExecuteAsync(featureId, forceRerun: true, ct);
                    break;
                case "devops":
                    await GetService<InvokeDevOpsAgentUseCase>().ExecuteAsync(featureId, forceRerun: true, ct);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unknown step '{step}'. Valid values: prd, architect, tasks, code, qa, devops.");
            }
            return;
        }

        switch (feature.Status)
        {
            case FeatureStatus.PrdPending:
                await GetService<InvokePrdAgentUseCase>().ExecuteAsync(featureId, ct: ct);
                break;

            case FeatureStatus.ArchPending:
                await GetService<InvokeArchitectAgentUseCase>().ExecuteAsync(featureId, ct: ct);
                break;

            case FeatureStatus.TasksPending:
                await GetService<InvokeTaskAgentUseCase>().ExecuteAsync(featureId, ct: ct);
                break;

            case FeatureStatus.Coding:
                await GetService<InvokeCodeAgentUseCase>().ExecuteAsync(featureId, ct: ct);
                break;

            case FeatureStatus.QaPending:
                await GetService<InvokeQaAgentUseCase>().ExecuteAsync(featureId, ct: ct);
                break;

            case FeatureStatus.QaApproved:
                await GetService<InvokeDevOpsAgentUseCase>().ExecuteAsync(featureId, ct: ct);
                break;

            default:
                throw new InvalidOperationException(
                    $"Feature is in '{feature.Status}' status — there is no step to re-run. " +
                    "Re-run is only available for pending or in-progress statuses.");
        }
    }

    private T GetService<T>() where T : notnull
    {
        return (T)_serviceProvider.GetService(typeof(T))!;
    }
}
