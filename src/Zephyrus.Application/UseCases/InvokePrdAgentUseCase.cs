using System.Diagnostics;
using Zephyrus.Core.Agents;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.UseCases;

/// <summary>
/// Orchestrates PRD generation: validates state, invokes the PRD Agent,
/// commits the output to the repo, and records the artifact.
/// </summary>
public sealed class InvokePrdAgentUseCase
{
    private readonly IFeatureRepository _featureRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IArtifactRepository _artifactRepository;
    private readonly IPipelineEventRepository _pipelineEventRepository;
    private readonly IAgent<PrdAgentInput, PrdAgentOutput> _prdAgent;
    private readonly ICodeHostFactory _codeHostFactory;
    private readonly IAgentInvocationRepository _agentInvocationRepository;

    public InvokePrdAgentUseCase(
        IFeatureRepository featureRepository,
        IProjectRepository projectRepository,
        IArtifactRepository artifactRepository,
        IPipelineEventRepository pipelineEventRepository,
        IAgent<PrdAgentInput, PrdAgentOutput> prdAgent,
        ICodeHostFactory codeHostFactory,
        IAgentInvocationRepository agentInvocationRepository)
    {
        _featureRepository = featureRepository;
        _projectRepository = projectRepository;
        _artifactRepository = artifactRepository;
        _pipelineEventRepository = pipelineEventRepository;
        _prdAgent = prdAgent;
        _codeHostFactory = codeHostFactory;
        _agentInvocationRepository = agentInvocationRepository;
    }

    public async Task<Artifact> ExecuteAsync(Guid featureId, bool forceRerun = false, CancellationToken ct = default)
    {
        var feature = await _featureRepository.GetByIdAsync(featureId, ct)
            ?? throw new InvalidOperationException($"Feature '{featureId}' not found.");

        var isRerun = feature.Status == FeatureStatus.PrdPending || forceRerun;

        if (!forceRerun && feature.Status != FeatureStatus.Ideation && !isRerun)
        {
            throw new InvalidOperationException(
                $"Feature must be in Ideation or PrdPending status to generate PRD. Current status: {feature.Status}.");
        }

        var project = await _projectRepository.GetByIdAsync(feature.ProjectId, ct)
            ?? throw new InvalidOperationException($"Project '{feature.ProjectId}' not found.");

        var featureSlug = GenerateSlug(feature.Prompt);

        if (isRerun)
        {
            // Clean up partial artifact from previous attempt
            var existing = await _artifactRepository.GetByFeatureIdAndTypeAsync(featureId, ArtifactType.Prd, ct);
            if (existing is not null)
                await _artifactRepository.DeleteAsync(existing, ct);
        }
        else
        {
            // Transition: Ideation → PrdPending
            var fromStatus = feature.Advance();
            await _featureRepository.UpdateAsync(feature, ct);
            await _pipelineEventRepository.AddAsync(
                PipelineEvent.Create(featureId, fromStatus, feature.Status, "system"), ct);
        }

        // Invoke the PRD Agent
        var agentInput = new PrdAgentInput
        {
            FeaturePrompt = feature.Prompt,
            ProjectConstitution = project.Config,
            FeatureSlug = featureSlug
        };

        var stopwatch = Stopwatch.StartNew();
        var agentOutput = await _prdAgent.RunAsync(agentInput, ct);
        stopwatch.Stop();

        await _agentInvocationRepository.AddAsync(
            AgentInvocation.Create(featureId, "prd",
                agentOutput.SystemPrompt, agentOutput.UserMessage, agentOutput.RawResponse,
                (int)stopwatch.ElapsedMilliseconds), ct);

        // Record artifact (generates GUID used for the file path)
        var artifact = Artifact.Create(featureId, ArtifactType.Prd);
        artifact.SetPendingContent(agentOutput.Markdown);
        await _artifactRepository.AddAsync(artifact, ct);

        // Commit PRD to repository
        var codeHost = _codeHostFactory.Create(project.GitHubToken);
        await codeHost.CommitFileAsync(
            project.RepositorySlug,
            "main",
            artifact.RepositoryPath,
            agentOutput.Markdown,
            $"[Zephyrus] Add PRD for {featureSlug}",
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

        // Keep only alphanumeric and hyphens
        slug = new string(slug.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());

        // Collapse consecutive hyphens and trim
        while (slug.Contains("--"))
            slug = slug.Replace("--", "-");

        slug = slug.Trim('-');

        // Truncate to reasonable length
        if (slug.Length > 50)
            slug = slug[..50].TrimEnd('-');

        return slug;
    }
}
