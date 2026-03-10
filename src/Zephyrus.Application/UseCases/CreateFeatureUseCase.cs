using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.UseCases;

public sealed class CreateFeatureUseCase
{
    private readonly IFeatureRepository _featureRepository;
    private readonly IProjectRepository _projectRepository;

    public CreateFeatureUseCase(IFeatureRepository featureRepository, IProjectRepository projectRepository)
    {
        _featureRepository = featureRepository;
        _projectRepository = projectRepository;
    }

    public async Task<Feature> ExecuteAsync(Guid projectId, string prompt, CancellationToken ct = default)
    {
        var project = await _projectRepository.GetByIdAsync(projectId, ct)
            ?? throw new ArgumentException($"Project '{projectId}' not found.");

        var feature = Feature.Create(project.Id, prompt);
        await _featureRepository.AddAsync(feature, ct);

        return feature;
    }
}
