using Zephyrus.Core.Interfaces.Repositories;

namespace Zephyrus.Application.UseCases.Features;

public class DeleteFeatureUseCase
{
    private readonly IFeatureRepository _featureRepository;

    public DeleteFeatureUseCase(IFeatureRepository featureRepository)
    {
        _featureRepository = featureRepository;
    }

    public async Task<DeleteFeatureResult> ExecuteAsync(Guid featureId)
    {
        var feature = await _featureRepository.GetByIdAsync(featureId);
        if (feature == null)
        {
            return DeleteFeatureResult.NotFound;
        }

        try
        {
            await _featureRepository.DeleteAsync(featureId);
            return DeleteFeatureResult.Success;
        }
        catch (InvalidOperationException)
        {
            return DeleteFeatureResult.Conflict;
        }
    }
}

public enum DeleteFeatureResult
{
    Success,
    NotFound,
    Conflict
}