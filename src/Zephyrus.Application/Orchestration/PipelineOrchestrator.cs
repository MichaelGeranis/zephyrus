using Microsoft.Extensions.Logging;
using Zephyrus.Core.Enums;
using Zephyrus.Application.UseCases;

namespace Zephyrus.Application.Orchestration;

/// <summary>
/// Deterministic orchestrator — not an AI. Reacts to approval events
/// by invoking the next agent in the pipeline. Agents are stateless;
/// all state lives in the database and GitHub.
/// </summary>
public sealed class PipelineOrchestrator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PipelineOrchestrator> _logger;

    public PipelineOrchestrator(
        IServiceProvider serviceProvider,
        ILogger<PipelineOrchestrator> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Called after an artifact is approved and the feature has advanced.
    /// Determines whether a follow-up agent should be triggered and invokes it.
    /// </summary>
    public async Task OnArtifactApprovedAsync(Guid featureId, FeatureStatus newStatus, CancellationToken ct = default)
    {
        switch (newStatus)
        {
            case FeatureStatus.PrdApproved:
                _logger.LogInformation("Feature {FeatureId}: PRD approved, triggering Architect Agent.", featureId);
                var architectUseCase = GetRequiredService<InvokeArchitectAgentUseCase>();
                await architectUseCase.ExecuteAsync(featureId, ct);
                break;

            case FeatureStatus.ArchApproved:
                _logger.LogInformation("Feature {FeatureId}: ADR approved, triggering Task Agent.", featureId);
                var taskUseCase = GetRequiredService<InvokeTaskAgentUseCase>();
                await taskUseCase.ExecuteAsync(featureId, ct);
                break;

            // Future steps will wire additional agents here:
            // case FeatureStatus.TasksApproved:
            //     → trigger Code Agents (Step 9)
            // case FeatureStatus.Coding (all PRs open):
            //     → trigger QA Agent (Step 10)

            default:
                _logger.LogDebug("Feature {FeatureId}: No follow-up agent for status {Status}.", featureId, newStatus);
                break;
        }
    }

    private T GetRequiredService<T>() where T : notnull
    {
        return (T)_serviceProvider.GetService(typeof(T))!;
    }
}
