using System.Diagnostics;
using Zephyrus.Application.Exceptions;
using Zephyrus.Core.Agents;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Interfaces;
using Zephyrus.Core.Pipeline;

namespace Zephyrus.Application.UseCases;

/// <summary>
/// Orchestrates QA: validates state, loads ADR and task context, invokes the
/// QA Agent, commits test files to PR branches, commits the report, and records
/// the Test artifact. Does NOT advance the feature — it is already at QaPending
/// when the orchestrator triggers this use case.
/// </summary>
public sealed class InvokeQaAgentUseCase
{
    private readonly IFeatureRepository _featureRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IArtifactRepository _artifactRepository;
    private readonly ITaskItemRepository _taskItemRepository;
    private readonly IAgent<QaAgentInput, QaAgentOutput> _qaAgent;
    private readonly ICodeHostFactory _codeHostFactory;
    private readonly IAgentInvocationRepository _agentInvocationRepository;

    public InvokeQaAgentUseCase(
        IFeatureRepository featureRepository,
        IProjectRepository projectRepository,
        IArtifactRepository artifactRepository,
        ITaskItemRepository taskItemRepository,
        IAgent<QaAgentInput, QaAgentOutput> qaAgent,
        ICodeHostFactory codeHostFactory,
        IAgentInvocationRepository agentInvocationRepository)
    {
        _featureRepository = featureRepository;
        _projectRepository = projectRepository;
        _artifactRepository = artifactRepository;
        _taskItemRepository = taskItemRepository;
        _qaAgent = qaAgent;
        _codeHostFactory = codeHostFactory;
        _agentInvocationRepository = agentInvocationRepository;
    }

    public async Task<Artifact> ExecuteAsync(Guid featureId, bool forceRerun = false, CancellationToken ct = default)
    {
        var feature = await _featureRepository.GetByIdAsync(featureId, ct)
            ?? throw new ResourceNotFoundException($"Feature '{featureId}' not found.");

        if (!forceRerun && feature.Status != FeatureStatus.QaPending)
        {
            throw new RequestRejectedException(
                $"Feature must be in QaPending status to run QA. Current status: {feature.Status}.");
        }

        // Clean up partial artifact from previous attempt (rerun)
        var existingTest = await _artifactRepository.GetByFeatureIdAndTypeAsync(featureId, ArtifactType.Test, ct);
        if (existingTest is not null)
            await _artifactRepository.DeleteAsync(existingTest, ct);

        var project = await _projectRepository.GetByIdAsync(feature.ProjectId, ct)
            ?? throw new ResourceNotFoundException($"Project '{feature.ProjectId}' not found.");

        // Load the approved ADR content
        var adrArtifact = await _artifactRepository.GetByFeatureIdAndTypeAsync(featureId, ArtifactType.Adr, ct)
            ?? throw new RequestRejectedException($"No approved ADR artifact found for feature '{featureId}'.");

        var codeHost = _codeHostFactory.Create(project.GitHubToken);
        var adrContent = await codeHost.GetFileContentAsync(
            project.RepositorySlug, "main", adrArtifact.RepositoryPath, ct)
            ?? throw new ResourceNotFoundException(
                $"ADR file not found at '{adrArtifact.RepositoryPath}' in repo '{project.RepositorySlug}'.");

        var featureSlug = GenerateSlug(feature.Prompt);

        // Get all tasks with PRs for this feature
        var tasks = await _taskItemRepository.GetByFeatureIdAsync(featureId, ct);
        var tasksWithPrs = tasks.Where(t => t.PrId.HasValue).ToList();

        // Build task context for the QA Agent
        var taskContexts = tasksWithPrs.Select(t => new QaTaskContext
        {
            TaskTitle = t.Title,
            PrId = t.PrId!.Value,
            BranchName = $"feature/{featureSlug}/{t.Id.ToString()[..8]}"
        }).ToList();

        // Invoke the QA Agent
        var agentInput = new QaAgentInput
        {
            FeatureSlug = featureSlug,
            ApprovedAdr = adrContent,
            ProjectConstitution = project.Config,
            Tasks = taskContexts
        };

        var stopwatch = Stopwatch.StartNew();
        var agentOutput = await _qaAgent.RunAsync(agentInput, ct);
        stopwatch.Stop();

        // Create the artifact first: its ID exists before it is saved, so the
        // run can record what it produced.
        var artifact = Artifact.Create(featureId, ArtifactType.Test);
        await _agentInvocationRepository.AddAsync(
            AgentInvocation.Create(featureId, "qa",
                agentOutput.SystemPrompt, agentOutput.UserMessage, agentOutput.RawResponse,
                (int)stopwatch.ElapsedMilliseconds, artifact.Id), ct);

        // Model output decides these paths; refuse protected ones before
        // anything is committed.
        GeneratedFilePolicy.EnsureAllowed("QA", agentOutput.TestFiles.Select(f => f.Path));

        // Commit test files to the first task's branch (or main if no branches)
        var targetBranch = taskContexts.Count > 0 ? taskContexts[0].BranchName : "main";
        foreach (var testFile in agentOutput.TestFiles)
        {
            await codeHost.CommitFileAsync(
                project.RepositorySlug,
                targetBranch,
                testFile.Path,
                testFile.Content,
                $"[Zephyrus] Add tests for {featureSlug}",
                ct);
        }

        artifact.SetPendingContent(agentOutput.ReportMarkdown);
        await _artifactRepository.AddAsync(artifact, ct);

        // Commit QA report to main
        await codeHost.CommitFileAsync(
            project.RepositorySlug,
            "main",
            artifact.RepositoryPath,
            agentOutput.ReportMarkdown,
            $"[Zephyrus] Add QA report for {featureSlug}",
            ct);

        artifact.MarkCommitSucceeded();
        await _artifactRepository.UpdateAsync(artifact, ct);

        return artifact;
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
