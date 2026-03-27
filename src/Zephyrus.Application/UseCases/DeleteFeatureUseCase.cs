using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.UseCases;

/// <summary>
/// Use case for deleting a feature with cascading artifact deletion.
/// Provides deletion summary before execution to show impact.
/// </summary>
public sealed class DeleteFeatureUseCase
{
    private readonly IFeatureRepository _featureRepository;
    private readonly IArtifactRepository _artifactRepository;

    public DeleteFeatureUseCase(
        IFeatureRepository featureRepository,
        IArtifactRepository artifactRepository)
    {
        _featureRepository = featureRepository;
        _artifactRepository = artifactRepository;
    }

    /// <summary>
    /// Gets a summary of what will be deleted when the feature is removed.
    /// </summary>
    /// <param name="featureId">The ID of the feature to analyze</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Deletion summary with feature name and artifact count</returns>
    public async Task<DeletionSummary> GetDeletionSummaryAsync(Guid featureId, CancellationToken ct = default)
    {
        var feature = await _featureRepository.GetByIdWithArtifactsAsync(featureId, ct);
        
        if (feature is null)
        {
            throw new InvalidOperationException($"Feature '{featureId}' not found.");
        }

        return new DeletionSummary(
            FeatureName: feature.Prompt,
            ArtifactCount: feature.Artifacts.Count);
    }

    /// <summary>
    /// Executes the feature deletion with cascading artifact removal.
    /// EF Core cascade delete configuration handles the related entities.
    /// </summary>
    /// <param name="featureId">The ID of the feature to delete</param>
    /// <param name="ct">Cancellation token</param>
    public async Task ExecuteAsync(Guid featureId, CancellationToken ct = default)
    {
        var feature = await _featureRepository.GetByIdAsync(featureId, ct);
        
        if (feature is null)
        {
            throw new InvalidOperationException($"Feature '{featureId}' not found.");
        }

        // EF cascade delete configuration will handle related artifacts, tasks, 
        // pipeline events, deployments, and agent invocations
        await _featureRepository.DeleteAsync(feature, ct);
    }
}

/// <summary>
/// Summary of what will be deleted when a feature is removed.
/// </summary>
/// <param name="FeatureName">The feature prompt/name</param>
/// <param name="ArtifactCount">Number of artifacts that will be deleted</param>
public record DeletionSummary(string FeatureName, int ArtifactCount);