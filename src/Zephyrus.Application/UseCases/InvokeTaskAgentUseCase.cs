using System.Diagnostics;
using Zephyrus.Core.Agents;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.UseCases;

/// <summary>
/// Orchestrates task breakdown: validates state, loads approved PRD and ADR,
/// invokes the Task Agent, creates GitHub Issues, records TaskItems in DB,
/// commits the task summary to the repo, and records the artifact.
/// </summary>
public sealed class InvokeTaskAgentUseCase
{
    private readonly IFeatureRepository _featureRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IArtifactRepository _artifactRepository;
    private readonly ITaskItemRepository _taskItemRepository;
    private readonly IPipelineEventRepository _pipelineEventRepository;
    private readonly IAgent<TaskAgentInput, TaskAgentOutput> _taskAgent;
    private readonly ICodeHostFactory _codeHostFactory;
    private readonly IAgentInvocationRepository _agentInvocationRepository;

    public InvokeTaskAgentUseCase(
        IFeatureRepository featureRepository,
        IProjectRepository projectRepository,
        IArtifactRepository artifactRepository,
        ITaskItemRepository taskItemRepository,
        IPipelineEventRepository pipelineEventRepository,
        IAgent<TaskAgentInput, TaskAgentOutput> taskAgent,
        ICodeHostFactory codeHostFactory,
        IAgentInvocationRepository agentInvocationRepository)
    {
        _featureRepository = featureRepository;
        _projectRepository = projectRepository;
        _artifactRepository = artifactRepository;
        _taskItemRepository = taskItemRepository;
        _pipelineEventRepository = pipelineEventRepository;
        _taskAgent = taskAgent;
        _codeHostFactory = codeHostFactory;
        _agentInvocationRepository = agentInvocationRepository;
    }

    public async Task<Artifact> ExecuteAsync(Guid featureId, CancellationToken ct = default)
    {
        var feature = await _featureRepository.GetByIdAsync(featureId, ct)
            ?? throw new InvalidOperationException($"Feature '{featureId}' not found.");

        if (feature.Status != FeatureStatus.ArchApproved)
        {
            throw new InvalidOperationException(
                $"Feature must be in ArchApproved status to generate tasks. Current status: {feature.Status}.");
        }

        var project = await _projectRepository.GetByIdAsync(feature.ProjectId, ct)
            ?? throw new InvalidOperationException($"Project '{feature.ProjectId}' not found.");

        // Load the approved PRD content
        var prdArtifact = await _artifactRepository.GetByFeatureIdAndTypeAsync(featureId, ArtifactType.Prd, ct)
            ?? throw new InvalidOperationException($"No approved PRD artifact found for feature '{featureId}'.");

        var codeHost = _codeHostFactory.Create(project.GitHubToken);
        var prdContent = await codeHost.GetFileContentAsync(
            project.RepositorySlug, "main", prdArtifact.RepositoryPath, ct)
            ?? throw new InvalidOperationException(
                $"PRD file not found at '{prdArtifact.RepositoryPath}' in repo '{project.RepositorySlug}'.");

        // Load the approved ADR content
        var adrArtifact = await _artifactRepository.GetByFeatureIdAndTypeAsync(featureId, ArtifactType.Adr, ct)
            ?? throw new InvalidOperationException($"No approved ADR artifact found for feature '{featureId}'.");

        var adrContent = await codeHost.GetFileContentAsync(
            project.RepositorySlug, "main", adrArtifact.RepositoryPath, ct)
            ?? throw new InvalidOperationException(
                $"ADR file not found at '{adrArtifact.RepositoryPath}' in repo '{project.RepositorySlug}'.");

        var featureSlug = GenerateSlug(feature.Prompt);

        // Transition: ArchApproved → TasksPending
        var fromStatus = feature.Advance();
        await _featureRepository.UpdateAsync(feature, ct);
        await _pipelineEventRepository.AddAsync(
            PipelineEvent.Create(featureId, fromStatus, feature.Status, "system"), ct);

        // Invoke the Task Agent
        var agentInput = new TaskAgentInput
        {
            ApprovedPrd = prdContent,
            ApprovedAdr = adrContent,
            ProjectConstitution = project.Config,
            FeatureSlug = featureSlug
        };

        var stopwatch = Stopwatch.StartNew();
        var agentOutput = await _taskAgent.RunAsync(agentInput, ct);
        stopwatch.Stop();

        await _agentInvocationRepository.AddAsync(
            AgentInvocation.Create(featureId, "task",
                agentOutput.SystemPrompt, agentOutput.UserMessage, agentOutput.RawResponse,
                (int)stopwatch.ElapsedMilliseconds), ct);

        // Create GitHub Issues and TaskItems for each task
        foreach (var taskDef in agentOutput.Tasks)
        {
            var issueId = await codeHost.CreateIssueAsync(
                project.RepositorySlug,
                taskDef.Title,
                taskDef.Body,
                new[] { $"agent:{taskDef.AgentType}", TruncateLabel($"feature:{featureSlug}") },
                ct);

            var taskItem = TaskItem.Create(featureId, taskDef.Title, taskDef.AgentType);
            taskItem.SetExternalIssueId(issueId);
            await _taskItemRepository.AddAsync(taskItem, ct);
        }

        // Record artifact (generates GUID used for the file path)
        var artifact = Artifact.Create(featureId, ArtifactType.Task);
        artifact.SetPendingContent(agentOutput.Markdown);
        await _artifactRepository.AddAsync(artifact, ct);

        // Commit task summary to repository
        await codeHost.CommitFileAsync(
            project.RepositorySlug,
            "main",
            artifact.RepositoryPath,
            agentOutput.Markdown,
            $"[Zephyrus] Add task breakdown for {featureSlug}",
            ct);

        artifact.MarkCommitSucceeded();
        await _artifactRepository.UpdateAsync(artifact, ct);

        return artifact;
    }

    private static string TruncateLabel(string label)
    {
        if (label.Length <= 50)
            return label;

        return label[..50].TrimEnd('-');
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
