using Zephyrus.Core.Entities;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.UseCases;

public sealed class GetFeatureByIdUseCase
{
    private readonly IFeatureRepository _featureRepository;

    public GetFeatureByIdUseCase(IFeatureRepository featureRepository)
    {
        _featureRepository = featureRepository;
    }

    public Task<Feature?> ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        return _featureRepository.GetByIdAsync(id, ct);
    }
}
