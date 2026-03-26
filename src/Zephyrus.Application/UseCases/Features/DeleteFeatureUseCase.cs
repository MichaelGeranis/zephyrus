using Zephyrus.Core.Interfaces.Repositories;
using Zephyrus.Core.Interfaces.UseCases.Features;

namespace Zephyrus.Application.UseCases.Features;

public class DeleteFeatureUseCase : IDeleteFeatureUseCase
{
    private readonly IFeatureRepository _featureRepository;

    public DeleteFeatureUseCase(IFeatureRepository featureRepository)
    {
        _featureRepository = featureRepository ?? throw new ArgumentNullException(nameof(featureRepository));
    }

    public async Task<bool> ExecuteAsync(Guid featureId, CancellationToken cancellationToken = default)
    {
        var feature = await _featureRepository.GetByIdAsync(featureId, cancellationToken);
        if (feature == null)
        {
            return false;
        }

        await _featureRepository.DeleteAsync(feature, cancellationToken);
        return true;
    }
}