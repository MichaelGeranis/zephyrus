using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.Managers;

public sealed class ArtifactManager
{
    private readonly IArtifactRepository _artifactRepository;
    private readonly IFeatureRepository _featureRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly ICodeHost _codeHost;

    public ArtifactManager(
        IArtifactRepository artifactRepository,
        IFeatureRepository featureRepository,
        IProjectRepository projectRepository,
        ICodeHost codeHost)
    {
        _artifactRepository = artifactRepository;
        _featureRepository = featureRepository;
        _projectRepository = projectRepository;
        _codeHost = codeHost;
    }

    public async Task<IReadOnlyList<Artifact>> GetByFeatureIdAsync(Guid featureId, CancellationToken ct = default)
    {
        var feature = await _featureRepository.GetByIdAsync(featureId, ct);
        if (feature is null)
            return null!;

        return await _artifactRepository.GetByFeatureIdAsync(featureId, ct);
    }

    public async Task<Artifact?> GetByIdAsync(Guid artifactId, CancellationToken ct = default)
    {
        return await _artifactRepository.GetByIdAsync(artifactId, ct);
    }

    public async Task<string?> GetContentAsync(Guid featureId, Guid artifactId, CancellationToken ct = default)
    {
        var feature = await _featureRepository.GetByIdAsync(featureId, ct);
        if (feature is null)
            return null;

        var artifact = await _artifactRepository.GetByIdAsync(artifactId, ct);
        if (artifact is null || artifact.FeatureId != featureId)
            return null;

        var project = await _projectRepository.GetByIdAsync(feature.ProjectId, ct);
        if (project is null)
            return null;

        return await _codeHost.GetFileContentAsync(
            project.RepositorySlug, "main", artifact.RepositoryPath, ct);
    }
}
