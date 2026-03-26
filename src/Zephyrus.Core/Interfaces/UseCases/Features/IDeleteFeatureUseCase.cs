namespace Zephyrus.Core.Interfaces.UseCases.Features;

public interface IDeleteFeatureUseCase
{
    Task<bool> ExecuteAsync(Guid featureId, CancellationToken cancellationToken = default);
}