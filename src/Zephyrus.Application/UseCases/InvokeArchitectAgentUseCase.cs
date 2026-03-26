using System.Diagnostics;
using Zephyrus.Core.Agents;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.UseCases;

/// <summary>
/// Orchestrates Architecture Decision Record generation: validates state,
/// loads the approved PRD, invokes the Architect Agent, commits the output
/// to the repo, and records the artifact.
/// </summary>
public sealed class InvokeArchitectAgentUseCase
{
    private readonly IFeatureRepository _featureRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IArtifactRepository _artifactRepository;
    private readonly IPipelineEventRepository _pipelineEventRepository;
    private readonly IAgent<ArchitectAgentInput, ArchitectAgentOutput> _architectAgent;
    private readonly ICodeHostFactory _codeHostFactory;
    private readonly IAgentInvocationRepository _agentInvocationRepository;

    public InvokeArchitectAgentUseCase(
        IFeatureRepository featureRepository,
        IProjectRepository projectRepository,
        IArtifactRepository artifactRepository,
        IPipelineEventRepository pipelineEventRepository,
        IAgent<ArchitectAgentInput, ArchitectAgentOutput> architectAgent,
        ICodeHostFactory codeHostFactory,
        IAgentInvocationRepository agentInvocationRepository)
    {
        _featureRepository = featureRepository;
        _projectRepository = projectRepository;
        _artifactRepository = artifactRepository;
        _pipelineEventRepository = pipelineEventRepository;
        _architectAgent = architectAgent;
        _codeHostFactory = codeHostFactory;
        _agentInvocationRepository = agentInvocationRepository;
    }

    public async Task<Artifact> ExecuteAsync(Guid featureId, CancellationToken ct = default)
    {
        var feature = await _featureRepository.GetByIdAsync(featureId, ct)
            ?? throw new InvalidOperationException($"Feature '{featureId}' not found.");

        var isRerun = feature.Status == FeatureStatus.ArchPending;

        if (feature.Status != FeatureStatus.PrdApproved && !isRerun)
        {
            throw new InvalidOperationException(
                $"Feature must be in PrdApproved or ArchPending status to generate ADR. Current status: {feature.Status}.");
        }

        var project = await _projectRepository.GetByIdAsync(feature.ProjectId, ct)
            ?? throw new InvalidOperationException($"Project '{feature.ProjectId}' not found.");

        // Load the approved PRD content from the repo
        var prdArtifact = await _artifactRepository.GetByFeatureIdAndTypeAsync(featureId, ArtifactType.Prd, ct)
            ?? throw new InvalidOperationException($"No approved PRD artifact found for feature '{featureId}'.");

        var codeHost = _codeHostFactory.Create(project.GitHubToken);
        var prdContent = await codeHost.GetFileContentAsync(
            project.RepositorySlug, "main", prdArtifact.RepositoryPath, ct)
            ?? throw new InvalidOperationException(
                $"PRD file not found at '{prdArtifact.RepositoryPath}' in repo '{project.RepositorySlug}'.");

        var featureSlug = GenerateSlug(feature.Prompt);

        if (isRerun)
        {
            var existing = await _artifactRepository.GetByFeatureIdAndTypeAsync(featureId, ArtifactType.Adr, ct);
            if (existing is not null)
                await _artifactRepository.DeleteAsync(existing, ct);
        }
        else
        {
            // Transition: PrdApproved → ArchPending
            var fromStatus = feature.Advance();
            await _featureRepository.UpdateAsync(feature, ct);
            await _pipelineEventRepository.AddAsync(
                PipelineEvent.Create(featureId, fromStatus, feature.Status, "system"), ct);
        }

        // Invoke the Architect Agent
        var agentInput = new ArchitectAgentInput
        {
            ApprovedPrd = prdContent,
            ProjectConstitution = project.Config,
            FeatureSlug = featureSlug
        };

        var stopwatch = Stopwatch.StartNew();
        var agentOutput = await _architectAgent.RunAsync(agentInput, ct);
        stopwatch.Stop();

        await _agentInvocationRepository.AddAsync(
            AgentInvocation.Create(featureId, "architect",
                agentOutput.SystemPrompt, agentOutput.UserMessage, agentOutput.RawResponse,
                (int)stopwatch.ElapsedMilliseconds), ct);

        // Record artifact (generates GUID used for the file path)
        var artifact = Artifact.Create(featureId, ArtifactType.Adr);
        artifact.SetPendingContent(agentOutput.Markdown);
        await _artifactRepository.AddAsync(artifact, ct);

        // Commit ADR to repository
        await codeHost.CommitFileAsync(
            project.RepositorySlug,
            "main",
            artifact.RepositoryPath,
            agentOutput.Markdown,
            $"[Zephyrus] Add ADR for {featureSlug}",
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
