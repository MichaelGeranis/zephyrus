using System.Diagnostics;
using Zephyrus.Core.Agents;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.UseCases;

/// <summary>
/// Orchestrates code generation for all tasks in a feature: validates state,
/// loads ADR context, invokes the Code Agent for each task, creates feature
/// branches and PRs, and records artifacts. Advances the feature to Coding.
/// </summary>
public sealed class InvokeCodeAgentUseCase
{
    private readonly IFeatureRepository _featureRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IArtifactRepository _artifactRepository;
    private readonly ITaskItemRepository _taskItemRepository;
    private readonly IPipelineEventRepository _pipelineEventRepository;
    private readonly IAgent<CodeAgentInput, CodeAgentOutput> _codeAgent;
    private readonly ICodeHost _codeHost;
    private readonly IAgentInvocationRepository _agentInvocationRepository;

    public InvokeCodeAgentUseCase(
        IFeatureRepository featureRepository,
        IProjectRepository projectRepository,
        IArtifactRepository artifactRepository,
        ITaskItemRepository taskItemRepository,
        IPipelineEventRepository pipelineEventRepository,
        IAgent<CodeAgentInput, CodeAgentOutput> codeAgent,
        ICodeHost codeHost,
        IAgentInvocationRepository agentInvocationRepository)
    {
        _featureRepository = featureRepository;
        _projectRepository = projectRepository;
        _artifactRepository = artifactRepository;
        _taskItemRepository = taskItemRepository;
        _pipelineEventRepository = pipelineEventRepository;
        _codeAgent = codeAgent;
        _codeHost = codeHost;
        _agentInvocationRepository = agentInvocationRepository;
    }

    public async Task ExecuteAsync(Guid featureId, CancellationToken ct = default)
    {
        var feature = await _featureRepository.GetByIdAsync(featureId, ct)
            ?? throw new InvalidOperationException($"Feature '{featureId}' not found.");

        if (feature.Status != FeatureStatus.TasksApproved)
        {
            throw new InvalidOperationException(
                $"Feature must be in TasksApproved status to generate code. Current status: {feature.Status}.");
        }

        var project = await _projectRepository.GetByIdAsync(feature.ProjectId, ct)
            ?? throw new InvalidOperationException($"Project '{feature.ProjectId}' not found.");

        // Load the approved ADR content
        var adrArtifact = await _artifactRepository.GetByFeatureIdAndTypeAsync(featureId, ArtifactType.Adr, ct)
            ?? throw new InvalidOperationException($"No approved ADR artifact found for feature '{featureId}'.");

        var adrContent = await _codeHost.GetFileContentAsync(
            project.RepositorySlug, "main", adrArtifact.RepositoryPath, ct)
            ?? throw new InvalidOperationException(
                $"ADR file not found at '{adrArtifact.RepositoryPath}' in repo '{project.RepositorySlug}'.");

        var featureSlug = GenerateSlug(feature.Prompt);

        // Transition: TasksApproved → Coding
        var fromStatus = feature.Advance();
        await _featureRepository.UpdateAsync(feature, ct);
        await _pipelineEventRepository.AddAsync(
            PipelineEvent.Create(featureId, fromStatus, feature.Status, "system"), ct);

        // Get all tasks for this feature
        var tasks = await _taskItemRepository.GetByFeatureIdAsync(featureId, ct);

        // Invoke Code Agent for each task, create branches and PRs
        foreach (var task in tasks)
        {
            var branchName = $"feature/{featureSlug}/{task.Id.ToString()[..8]}";

            // Create feature branch
            await _codeHost.CreateBranchAsync(
                project.RepositorySlug, branchName, "main", ct);

            // Mark task in progress
            task.MarkInProgress();
            await _taskItemRepository.UpdateAsync(task, ct);

            // Invoke Code Agent
            var agentInput = new CodeAgentInput
            {
                TaskTitle = task.Title,
                TaskBody = $"GitHub Issue #{task.ExternalIssueId}",
                ApprovedAdr = adrContent,
                ProjectConstitution = project.Config,
                FeatureSlug = featureSlug,
                BranchName = branchName
            };

            var stopwatch = Stopwatch.StartNew();
            var agentOutput = await _codeAgent.RunAsync(agentInput, ct);
            stopwatch.Stop();

            await _agentInvocationRepository.AddAsync(
                AgentInvocation.Create(featureId, "code",
                    agentOutput.SystemPrompt, agentOutput.UserMessage, agentOutput.RawResponse,
                    (int)stopwatch.ElapsedMilliseconds), ct);

            // Commit generated files to the feature branch
            foreach (var file in agentOutput.Files)
            {
                await _codeHost.CommitFileAsync(
                    project.RepositorySlug,
                    branchName,
                    file.Path,
                    file.Content,
                    $"[Zephyrus] {task.Title}",
                    ct);
            }

            // Open PR linked to the issue
            var prTitle = $"[Zephyrus] {task.Title} (#{task.ExternalIssueId})";
            var prBody = $"Closes #{task.ExternalIssueId}\n\nGenerated by Zephyrus Code Agent.";

            var prId = await _codeHost.CreatePullRequestAsync(
                project.RepositorySlug, branchName, "main", prTitle, prBody, ct);

            // Link PR to task
            task.SetPrId(prId);
            await _taskItemRepository.UpdateAsync(task, ct);
        }

        // Record a single Pr artifact for the feature
        var artifact = Artifact.Create(
            featureId,
            ArtifactType.Pr,
            $"pulls/feature-{featureSlug}");
        await _artifactRepository.AddAsync(artifact, ct);
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
