using Zephyrus.Core.Agents;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.UseCases;

/// <summary>
/// Orchestrates DevOps: validates state, loads project constitution, invokes the
/// DevOps Agent, commits the workflow file to GitHub, and records the Workflow
/// artifact. Does NOT advance the feature — it is already at QaApproved when the
/// orchestrator triggers this use case.
/// </summary>
public sealed class InvokeDevOpsAgentUseCase
{
    private readonly IFeatureRepository _featureRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IArtifactRepository _artifactRepository;
    private readonly IAgent<DevOpsAgentInput, DevOpsAgentOutput> _devOpsAgent;
    private readonly ICodeHost _codeHost;

    public InvokeDevOpsAgentUseCase(
        IFeatureRepository featureRepository,
        IProjectRepository projectRepository,
        IArtifactRepository artifactRepository,
        IAgent<DevOpsAgentInput, DevOpsAgentOutput> devOpsAgent,
        ICodeHost codeHost)
    {
        _featureRepository = featureRepository;
        _projectRepository = projectRepository;
        _artifactRepository = artifactRepository;
        _devOpsAgent = devOpsAgent;
        _codeHost = codeHost;
    }

    public async Task<Artifact> ExecuteAsync(Guid featureId, CancellationToken ct = default)
    {
        var feature = await _featureRepository.GetByIdAsync(featureId, ct)
            ?? throw new InvalidOperationException($"Feature '{featureId}' not found.");

        if (feature.Status != FeatureStatus.QaApproved)
        {
            throw new InvalidOperationException(
                $"Feature must be in QaApproved status to run DevOps. Current status: {feature.Status}.");
        }

        var project = await _projectRepository.GetByIdAsync(feature.ProjectId, ct)
            ?? throw new InvalidOperationException($"Project '{feature.ProjectId}' not found.");

        var featureSlug = GenerateSlug(feature.Prompt);

        // Extract deployment target from constitution (default to "Railway" if not specified)
        var deploymentTarget = ExtractDeploymentTarget(project.Config);

        // Invoke the DevOps Agent
        var agentInput = new DevOpsAgentInput
        {
            FeatureSlug = featureSlug,
            ProjectConstitution = project.Config,
            DeploymentTarget = deploymentTarget,
            RepositorySlug = project.RepositorySlug
        };

        var agentOutput = await _devOpsAgent.RunAsync(agentInput, ct);

        // Commit workflow file to main
        await _codeHost.CommitFileAsync(
            project.RepositorySlug,
            "main",
            agentOutput.RepositoryPath,
            agentOutput.WorkflowYaml,
            $"[Zephyrus] Add CI/CD workflow for {featureSlug}",
            ct);

        // Record Workflow artifact
        var artifact = Artifact.Create(featureId, ArtifactType.Workflow, agentOutput.RepositoryPath);
        await _artifactRepository.AddAsync(artifact, ct);

        return artifact;
    }

    private static string ExtractDeploymentTarget(string constitution)
    {
        // Simple extraction — look for "target:" in the YAML constitution
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

        return "Railway";
    }

    private static string GenerateSlug(string prompt)
    {
        var slug = prompt.ToLowerInvariant()
            .Replace(' ', '-')
            .Replace('\t', '-')
            .Replace('\n', '-');

        slug = new string(slug.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());

        while (slug.Contains("--"))
            slug = slug.Replace("--", "-");

        slug = slug.Trim('-');

        if (slug.Length > 60)
            slug = slug[..60].TrimEnd('-');

        return slug;
    }
}
