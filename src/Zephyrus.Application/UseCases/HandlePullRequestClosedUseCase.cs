using Microsoft.Extensions.Logging;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.UseCases;

/// <summary>
/// Reacts to a pull request closing on the code host. A merged pull request
/// completes its task and records the merged commit as a pending deployment,
/// which is what a later deployment status is matched against.
/// </summary>
public sealed class HandlePullRequestClosedUseCase
{
    private readonly IProjectRepository _projectRepository;
    private readonly ITaskItemRepository _taskItemRepository;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly ILogger<HandlePullRequestClosedUseCase> _logger;

    public HandlePullRequestClosedUseCase(
        IProjectRepository projectRepository,
        ITaskItemRepository taskItemRepository,
        IDeploymentRepository deploymentRepository,
        ILogger<HandlePullRequestClosedUseCase> logger)
    {
        _projectRepository = projectRepository;
        _taskItemRepository = taskItemRepository;
        _deploymentRepository = deploymentRepository;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        string repositorySlug,
        int prNumber,
        bool merged,
        string? mergeCommitSha,
        CancellationToken ct = default)
    {
        var project = await _projectRepository.GetByRepositorySlugAsync(repositorySlug, ct);
        if (project is null)
        {
            _logger.LogDebug("No project for repository {Repository}; ignoring pull request {Pr}.",
                repositorySlug, prNumber);
            return;
        }

        var task = await _taskItemRepository.GetByPullRequestAsync(project.Id, prNumber, ct);
        if (task is null)
        {
            _logger.LogDebug("No task tracks pull request {Pr} in {Repository}; ignoring.",
                prNumber, repositorySlug);
            return;
        }

        if (!merged)
        {
            _logger.LogInformation("Pull request {Pr} closed without merging; task {Task} left open.",
                prNumber, task.Id);
            return;
        }

        task.MarkDone();
        await _taskItemRepository.UpdateAsync(task, ct);

        if (string.IsNullOrWhiteSpace(mergeCommitSha))
        {
            _logger.LogWarning("Pull request {Pr} merged without a merge commit sha; no deployment recorded.",
                prNumber);
            return;
        }

        // Already recorded (a redelivered webhook, or another task merged at the
        // same commit) — nothing more to do.
        if (await _deploymentRepository.GetByShaAsync(mergeCommitSha, ct) is not null)
            return;

        var environment = ExtractDeploymentTarget(project.Config);
        await _deploymentRepository.AddAsync(
            Deployment.Create(task.FeatureId, mergeCommitSha, environment), ct);

        _logger.LogInformation(
            "Feature {FeatureId}: recorded pending deployment of {Sha} to {Environment}.",
            task.FeatureId, mergeCommitSha, environment);
    }

    /// <summary>
    /// Reads the deployment target from the Project Constitution, matching how
    /// the DevOps Agent picks its target.
    /// </summary>
    private static string ExtractDeploymentTarget(string constitution)
    {
        foreach (var line in constitution.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("target:", StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed["target:".Length..].Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
        }

        return "production";
    }
}
