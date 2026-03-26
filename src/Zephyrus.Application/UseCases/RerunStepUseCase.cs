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

    public async Task ExecuteAsync(Guid featureId, CancellationToken ct = default)
    {
        var feature = await _featureRepository.GetByIdAsync(featureId, ct)
            ?? throw new InvalidOperationException($"Feature '{featureId}' not found.");

        switch (feature.Status)
        {
            case FeatureStatus.PrdPending:
                await GetService<InvokePrdAgentUseCase>().ExecuteAsync(featureId, ct);
                break;

            case FeatureStatus.ArchPending:
                await GetService<InvokeArchitectAgentUseCase>().ExecuteAsync(featureId, ct);
                break;

            case FeatureStatus.TasksPending:
                await GetService<InvokeTaskAgentUseCase>().ExecuteAsync(featureId, ct);
                break;

            case FeatureStatus.Coding:
                await GetService<InvokeCodeAgentUseCase>().ExecuteAsync(featureId, ct);
                break;

            case FeatureStatus.QaPending:
                await GetService<InvokeQaAgentUseCase>().ExecuteAsync(featureId, ct);
                break;

            case FeatureStatus.QaApproved:
                await GetService<InvokeDevOpsAgentUseCase>().ExecuteAsync(featureId, ct);
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
