using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.Managers;

public sealed class FeatureManager
{
    private readonly IFeatureRepository _featureRepository;
    private readonly IProjectRepository _projectRepository;

    public FeatureManager(IFeatureRepository featureRepository, IProjectRepository projectRepository)
    {
        _featureRepository = featureRepository;
        _projectRepository = projectRepository;
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
}
