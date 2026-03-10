using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.UseCases;

public sealed class GetFeaturesByProjectUseCase
{
    private readonly IFeatureRepository _featureRepository;

    public GetFeaturesByProjectUseCase(IFeatureRepository featureRepository)
    {
        _featureRepository = featureRepository;
    }

    public Task<IReadOnlyList<Feature>> ExecuteAsync(Guid projectId, CancellationToken ct = default)
    {
        return _featureRepository.GetByProjectIdAsync(projectId, ct);
    }
}
