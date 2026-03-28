using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.Managers;

public sealed class FeatureManager
{
    private readonly IFeatureRepository _featureRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IArtifactRepository _artifactRepository;

    public FeatureManager(
        IFeatureRepository featureRepository,
        IProjectRepository projectRepository,
        IArtifactRepository artifactRepository)
    {
        _featureRepository = featureRepository;
        _projectRepository = projectRepository;
        _artifactRepository = artifactRepository;
    }

    public async Task<Feature> CreateAsync(Guid projectId, string prompt, CancellationToken ct = default)
    {
        var project = await _projectRepository.GetByIdAsync(projectId, ct)
            ?? throw new ArgumentException($"Project '{projectId}' not found.");

        var feature = Feature.Create(project.Id, prompt);
        await _featureRepository.AddAsync(feature, ct);

        return feature;
    }

    public Task<Feature?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return _featureRepository.GetByIdAsync(id, ct);
    }

    public Task<IReadOnlyList<Feature>> GetByProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        return _featureRepository.GetByProjectIdAsync(projectId, ct);
    }

    public async Task<DeletionPreview> GetDeletionPreviewAsync(Guid id, CancellationToken ct = default)
    {
        var feature = await _featureRepository.GetByIdAsync(id, ct)
            ?? throw new ArgumentException($"Feature '{id}' not found.");

        var artifacts = await _artifactRepository.GetByFeatureIdAsync(id, ct);
        var artifactCount = artifacts.Count;

        var warnings = artifactCount > 0
            ? new[] { $"This will permanently delete {artifactCount} artifact(s) and all associated tasks and events." }
            : Array.Empty<string>();

        return new DeletionPreview(feature.Prompt, artifactCount, warnings);
    }

    public async Task<int> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var feature = await _featureRepository.GetByIdAsync(id, ct)
            ?? throw new ArgumentException($"Feature '{id}' not found.");

        var artifacts = await _artifactRepository.GetByFeatureIdAsync(id, ct);
        var childCount = artifacts.Count;

        await _featureRepository.DeleteAsync(feature, ct);

        return 1 + childCount;
    }
}
