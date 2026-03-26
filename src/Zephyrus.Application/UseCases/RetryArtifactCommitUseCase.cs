using Zephyrus.Core.Entities;
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

    public RetryArtifactCommitUseCase(
        IArtifactRepository artifactRepository,
        IFeatureRepository featureRepository,
        IProjectRepository projectRepository,
        ICodeHostFactory codeHostFactory)
    {
        _artifactRepository = artifactRepository;
        _featureRepository = featureRepository;
        _projectRepository = projectRepository;
        _codeHostFactory = codeHostFactory;
    }

    public async Task<Artifact> ExecuteAsync(Guid featureId, Guid artifactId, CancellationToken ct = default)
    {
        var artifact = await _artifactRepository.GetByIdAsync(artifactId, ct)
            ?? throw new ArtifactNotFoundException(artifactId);

        if (artifact.FeatureId != featureId)
            throw new InvalidOperationException("Artifact does not belong to the specified feature.");

        if (artifact.CommitSucceeded)
            throw new InvalidOperationException("Artifact has already been committed successfully.");

        if (string.IsNullOrEmpty(artifact.PendingContent))
            throw new InvalidOperationException("No pending content available for retry.");

        var feature = await _featureRepository.GetByIdAsync(featureId, ct)
            ?? throw new InvalidOperationException($"Feature '{featureId}' not found.");

        var project = await _projectRepository.GetByIdAsync(feature.ProjectId, ct)
            ?? throw new InvalidOperationException($"Project '{feature.ProjectId}' not found.");

        var codeHost = _codeHostFactory.Create(project.GitHubToken);

        var featureSlug = GenerateSlug(feature.Prompt);
        var commitMessage = $"[Zephyrus] Retry commit for {artifact.Type} - {featureSlug}";

        await codeHost.CommitFileAsync(
            project.RepositorySlug,
            "main",
            artifact.RepositoryPath,
            artifact.PendingContent,
            commitMessage,
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
