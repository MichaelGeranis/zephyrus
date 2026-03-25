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
    private readonly ICodeHost _codeHost;
    private readonly IAgentInvocationRepository _agentInvocationRepository;

    public InvokePrdAgentUseCase(
        IFeatureRepository featureRepository,
        IProjectRepository projectRepository,
        IArtifactRepository artifactRepository,
        IPipelineEventRepository pipelineEventRepository,
        IAgent<PrdAgentInput, PrdAgentOutput> prdAgent,
        ICodeHost codeHost,
        IAgentInvocationRepository agentInvocationRepository)
    {
        _featureRepository = featureRepository;
        _projectRepository = projectRepository;
        _artifactRepository = artifactRepository;
        _pipelineEventRepository = pipelineEventRepository;
        _prdAgent = prdAgent;
        _codeHost = codeHost;
        _agentInvocationRepository = agentInvocationRepository;
    }

    public async Task<Artifact> ExecuteAsync(Guid featureId, CancellationToken ct = default)
    {
        var feature = await _featureRepository.GetByIdAsync(featureId, ct)
            ?? throw new InvalidOperationException($"Feature '{featureId}' not found.");

        if (feature.Status != FeatureStatus.Ideation)
        {
            throw new InvalidOperationException(
                $"Feature must be in Ideation status to generate PRD. Current status: {feature.Status}.");
        }

        var project = await _projectRepository.GetByIdAsync(feature.ProjectId, ct)
            ?? throw new InvalidOperationException($"Project '{feature.ProjectId}' not found.");

        var featureSlug = GenerateSlug(feature.Prompt);

        // Transition: Ideation → PrdPending
        var fromStatus = feature.Advance();
        await _featureRepository.UpdateAsync(feature, ct);
        await _pipelineEventRepository.AddAsync(
            PipelineEvent.Create(featureId, fromStatus, feature.Status, "system"), ct);

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

        // Commit PRD to repository
        await _codeHost.CommitFileAsync(
            project.RepositorySlug,
            "main",
            agentOutput.RepositoryPath,
            agentOutput.Markdown,
            $"[Zephyrus] Add PRD for {featureSlug}",
            ct);

        // Record artifact
        var artifact = Artifact.Create(featureId, ArtifactType.Prd, agentOutput.RepositoryPath);
        await _artifactRepository.AddAsync(artifact, ct);

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
        if (slug.Length > 60)
            slug = slug[..60].TrimEnd('-');

        return slug;
    }
}
