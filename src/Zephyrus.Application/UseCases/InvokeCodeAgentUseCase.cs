using System.Diagnostics;
using Zephyrus.Core.Agents;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.UseCases;

/// <summary>
/// Orchestrates code generation for all tasks in a feature: validates state,
/// loads ADR context, invokes the Code Agent in a multi-pass loop for each task,
/// creates feature branches and PRs, and records artifacts. Advances the feature to Coding.
/// </summary>
public sealed class InvokeCodeAgentUseCase
{
    private const int MaxFileRequestRounds = 3;

    private readonly IFeatureRepository _featureRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IArtifactRepository _artifactRepository;
    private readonly ITaskItemRepository _taskItemRepository;
    private readonly IPipelineEventRepository _pipelineEventRepository;
    private readonly IAgent<CodeAgentInput, CodeAgentOutput> _codeAgent;
    private readonly ICodeHostFactory _codeHostFactory;
    private readonly IAgentInvocationRepository _agentInvocationRepository;

    public InvokeCodeAgentUseCase(
        IFeatureRepository featureRepository,
        IProjectRepository projectRepository,
        IArtifactRepository artifactRepository,
        ITaskItemRepository taskItemRepository,
        IPipelineEventRepository pipelineEventRepository,
        IAgent<CodeAgentInput, CodeAgentOutput> codeAgent,
        ICodeHostFactory codeHostFactory,
        IAgentInvocationRepository agentInvocationRepository)
    {
        _featureRepository = featureRepository;
        _projectRepository = projectRepository;
        _artifactRepository = artifactRepository;
        _taskItemRepository = taskItemRepository;
        _pipelineEventRepository = pipelineEventRepository;
        _codeAgent = codeAgent;
        _codeHostFactory = codeHostFactory;
        _agentInvocationRepository = agentInvocationRepository;
    }

    public async Task ExecuteAsync(Guid featureId, CancellationToken ct = default)
    {
        var feature = await _featureRepository.GetByIdAsync(featureId, ct)
            ?? throw new InvalidOperationException($"Feature '{featureId}' not found.");

        var isRerun = feature.Status == FeatureStatus.Coding;

        if (feature.Status != FeatureStatus.TasksApproved && !isRerun)
        {
            throw new InvalidOperationException(
                $"Feature must be in TasksApproved or Coding status to generate code. Current status: {feature.Status}.");
        }

        var project = await _projectRepository.GetByIdAsync(feature.ProjectId, ct)
            ?? throw new InvalidOperationException($"Project '{feature.ProjectId}' not found.");

        // Load the approved ADR content
        var adrArtifact = await _artifactRepository.GetByFeatureIdAndTypeAsync(featureId, ArtifactType.Adr, ct)
            ?? throw new InvalidOperationException($"No approved ADR artifact found for feature '{featureId}'.");

        var codeHost = _codeHostFactory.Create(project.GitHubToken);
        var adrContent = await codeHost.GetFileContentAsync(
            project.RepositorySlug, "main", adrArtifact.RepositoryPath, ct)
            ?? throw new InvalidOperationException(
                $"ADR file not found at '{adrArtifact.RepositoryPath}' in repo '{project.RepositorySlug}'.");

        // Load codebase map (optional — null if not present)
        var codebaseMap = await codeHost.GetFileContentAsync(
            project.RepositorySlug, "main", "CODEBASE.md", ct);

        var featureSlug = GenerateSlug(feature.Prompt);

        if (isRerun)
        {
            var existing = await _artifactRepository.GetByFeatureIdAndTypeAsync(featureId, ArtifactType.Pr, ct);
            if (existing is not null)
                await _artifactRepository.DeleteAsync(existing, ct);
        }
        else
        {
            // Transition: TasksApproved → Coding
            var fromStatus = feature.Advance();
            await _featureRepository.UpdateAsync(feature, ct);
            await _pipelineEventRepository.AddAsync(
                PipelineEvent.Create(featureId, fromStatus, feature.Status, "system"), ct);
        }

        // Get all tasks for this feature
        var tasks = await _taskItemRepository.GetByFeatureIdAsync(featureId, ct);

        // Invoke Code Agent for each task, create branches and PRs
        foreach (var task in tasks)
        {
            // Skip tasks that already have a PR — supports resuming after a crash
            if (task.PrId.HasValue)
                continue;

            var branchName = $"feature/{featureSlug}/{task.Id.ToString()[..8]}";

            // Create feature branch
            await codeHost.CreateBranchAsync(
                project.RepositorySlug, branchName, "main", ct);

            // Mark task in progress
            task.MarkInProgress();
            await _taskItemRepository.UpdateAsync(task, ct);

            // Fetch task body from GitHub (source of truth)
            var taskBody = string.Empty;
            if (task.ExternalIssueId.HasValue)
            {
                var (_, issueBody) = await codeHost.GetIssueContentAsync(
                    project.RepositorySlug, task.ExternalIssueId.Value, ct);
                taskBody = issueBody;
            }

            // Multi-pass Code Agent loop
            var agentOutput = await RunCodeAgentWithContextLoop(
                task.Title, taskBody, adrContent, project.Config, featureSlug, branchName,
                codebaseMap, project.RepositorySlug, codeHost, ct);

            await _agentInvocationRepository.AddAsync(
                AgentInvocation.Create(featureId, "code",
                    agentOutput.FinalOutput.SystemPrompt, agentOutput.FinalOutput.UserMessage,
                    agentOutput.FinalOutput.RawResponse, (int)agentOutput.ElapsedMilliseconds), ct);

            // Commit generated files to the feature branch
            foreach (var file in agentOutput.FinalOutput.Files)
            {
                await codeHost.CommitFileAsync(
                    project.RepositorySlug,
                    branchName,
                    file.Path,
                    file.Content,
                    $"[Zephyrus] {task.Title}",
                    ct);
            }

            if (agentOutput.FinalOutput.Files.Count == 0)
                continue;

            // Open PR linked to the issue
            var prTitle = $"[Zephyrus] {task.Title} (#{task.ExternalIssueId})";
            var prBody = $"Closes #{task.ExternalIssueId}\n\nGenerated by Zephyrus Code Agent.";

            var prId = await codeHost.CreatePullRequestAsync(
                project.RepositorySlug, branchName, "main", prTitle, prBody, ct);

            // Link PR to task
            task.SetPrId(prId);
            await _taskItemRepository.UpdateAsync(task, ct);
        }

        // Record a single Pr artifact for the feature
        var artifact = Artifact.Create(featureId, ArtifactType.Pr);
        await _artifactRepository.AddAsync(artifact, ct);
    }

    private async Task<CodeAgentLoopResult> RunCodeAgentWithContextLoop(
        string taskTitle, string taskBody, string adrContent, string constitution,
        string featureSlug, string branchName, string? codebaseMap,
        string repoSlug, ICodeHost codeHost, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var conversationHistory = new List<ConversationMessage>();

        // First pass — no conversation history (agent builds initial message)
        var input = new CodeAgentInput
        {
            TaskTitle = taskTitle,
            TaskBody = taskBody,
            ApprovedAdr = adrContent,
            ProjectConstitution = constitution,
            FeatureSlug = featureSlug,
            BranchName = branchName,
            CodebaseMap = codebaseMap
        };

        var output = await _codeAgent.RunAsync(input, ct);

        for (var round = 0; round < MaxFileRequestRounds && output.Action == "request_files"; round++)
        {
            // Build the initial user message for conversation history on first round
            if (conversationHistory.Count == 0)
            {
                conversationHistory.Add(new ConversationMessage("user", output.UserMessage));
            }

            // Add the assistant's file request to history
            conversationHistory.Add(new ConversationMessage("assistant", output.RawResponse));

            // Fetch requested files from the repository
            var fileContents = await FetchRequestedFiles(
                repoSlug, "main", output.RequestedFiles, codeHost, ct);

            // Add file contents as user message
            conversationHistory.Add(new ConversationMessage("user", fileContents));

            // Next pass with full conversation history
            input = new CodeAgentInput
            {
                TaskTitle = taskTitle,
                TaskBody = taskBody,
                ApprovedAdr = adrContent,
                ProjectConstitution = constitution,
                FeatureSlug = featureSlug,
                BranchName = branchName,
                CodebaseMap = codebaseMap,
                ConversationHistory = conversationHistory
            };

            output = await _codeAgent.RunAsync(input, ct);
        }

        stopwatch.Stop();

        return new CodeAgentLoopResult(output, stopwatch.ElapsedMilliseconds);
    }

    private static async Task<string> FetchRequestedFiles(
        string repoSlug, string branch, IReadOnlyList<string> filePaths,
        ICodeHost codeHost, CancellationToken ct)
    {
        var sections = new List<string>();

        foreach (var path in filePaths)
        {
            var content = await codeHost.GetFileContentAsync(repoSlug, branch, path, ct);
            if (content is not null)
            {
                sections.Add($"## File: {path}\n```\n{content}\n```");
            }
            else
            {
                sections.Add($"## File: {path}\n(file not found)");
            }
        }

        return string.Join("\n\n", sections);
    }

    private sealed record CodeAgentLoopResult(CodeAgentOutput FinalOutput, long ElapsedMilliseconds);

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
