using Zephyrus.Application.Exceptions;
using Zephyrus.Core.Entities;
using Zephyrus.Core.Enums;
using Zephyrus.Core.Exceptions;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.UseCases;

/// <summary>
/// Retries only the GitHub commit for an artifact whose agent invocation
/// succeeded but whose commit to the code host failed.
/// </summary>
public sealed class RetryArtifactCommitUseCase
{
    private readonly IArtifactRepository _artifactRepository;
    private readonly IFeatureRepository _featureRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ICodeHostFactory _codeHostFactory;
    private readonly IAgentInvocationRepository _agentInvocationRepository;

    public RetryArtifactCommitUseCase(
        IArtifactRepository artifactRepository,
        IFeatureRepository featureRepository,
        IProjectRepository projectRepository,
        ICodeHostFactory codeHostFactory,
        IAgentInvocationRepository agentInvocationRepository)
    {
        _artifactRepository = artifactRepository;
        _featureRepository = featureRepository;
        _projectRepository = projectRepository;
        _codeHostFactory = codeHostFactory;
        _agentInvocationRepository = agentInvocationRepository;
    }

    public async Task<Artifact> ExecuteAsync(Guid featureId, Guid artifactId, CancellationToken ct = default)
    {
        var artifact = await _artifactRepository.GetByIdAsync(artifactId, ct)
            ?? throw new ArtifactNotFoundException(artifactId);

        if (artifact.FeatureId != featureId)
            throw new RequestRejectedException("Artifact does not belong to the specified feature.");

        if (artifact.CommitSucceeded)
            throw new RequestRejectedException("Artifact has already been committed successfully.");

        // Use PendingContent if available, otherwise fall back to agent invocation response
        var content = artifact.PendingContent;
        if (string.IsNullOrEmpty(content))
        {
            content = await ResolveContentFromInvocationAsync(featureId, artifact.Type, ct);
        }

        if (string.IsNullOrEmpty(content))
            throw new RequestRejectedException("No pending content available for retry.");

        var feature = await _featureRepository.GetByIdAsync(featureId, ct)
            ?? throw new ResourceNotFoundException($"Feature '{featureId}' not found.");

        var project = await _projectRepository.GetByIdAsync(feature.ProjectId, ct)
            ?? throw new ResourceNotFoundException($"Project '{feature.ProjectId}' not found.");

        var codeHost = _codeHostFactory.Create(project.GitHubToken);

        var featureSlug = GenerateSlug(feature.Prompt);
        var commitMessage = $"[Zephyrus] Retry commit for {artifact.Type} - {featureSlug}";

        await codeHost.CommitFileAsync(
            project.RepositorySlug,
            "main",
            artifact.RepositoryPath,
            content,
            commitMessage,
            ct);

        artifact.MarkCommitSucceeded();
        await _artifactRepository.UpdateAsync(artifact, ct);

        return artifact;
    }

    private static readonly Dictionary<ArtifactType, string> ArtifactTypeToAgentName = new()
    {
        { ArtifactType.Prd, "prd" },
        { ArtifactType.Adr, "architect" },
        { ArtifactType.Task, "task" },
        { ArtifactType.Test, "qa" },
        { ArtifactType.Workflow, "devops" },
    };

    private async Task<string?> ResolveContentFromInvocationAsync(
        Guid featureId, ArtifactType artifactType, CancellationToken ct)
    {
        if (!ArtifactTypeToAgentName.TryGetValue(artifactType, out var agentName))
            return null;

        var invocations = await _agentInvocationRepository.GetByFeatureIdAsync(featureId, ct);
        var invocation = invocations
            .Where(i => i.AgentName == agentName)
            .OrderByDescending(i => i.InvokedAt)
            .FirstOrDefault();

        return invocation?.Response;
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
