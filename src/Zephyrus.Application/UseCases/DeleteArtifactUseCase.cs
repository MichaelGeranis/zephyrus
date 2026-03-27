using Zephyrus.Core.Entities;
using Zephyrus.Core.Exceptions;
using Zephyrus.Core.Interfaces;

namespace Zephyrus.Application.UseCases;

/// <summary>
/// Handles the deletion of an individual artifact.
/// Validates the artifact exists and can be safely deleted before removing it.
/// </summary>
public sealed class DeleteArtifactUseCase
{
    private readonly IArtifactRepository _artifactRepository;

    public DeleteArtifactUseCase(IArtifactRepository artifactRepository)
    {
        _artifactRepository = artifactRepository;
    }

    /// <summary>
    /// Deletes the specified artifact after validating it exists.
    /// </summary>
    /// <param name="artifactId">The ID of the artifact to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the deletion operation.</returns>
    /// <exception cref="ArtifactNotFoundException">Thrown when the artifact is not found.</exception>
    /// <exception cref="InvalidOperationException">Thrown when database operation fails.</exception>
    public async Task ExecuteAsync(Guid artifactId, CancellationToken ct = default)
    {
        // Validate artifact exists
        var artifact = await _artifactRepository.GetByIdAsync(artifactId, ct)
            ?? throw new ArtifactNotFoundException(artifactId);

        try
        {
            // Delete the artifact
            await _artifactRepository.DeleteAsync(artifact, ct);
        }
        catch (Exception ex) when (!(ex is ArtifactNotFoundException))
        {
            // Wrap database exceptions in business exception
            throw new InvalidOperationException(
                $"Failed to delete artifact '{artifactId}'. {ex.Message}", ex);
        }
    }
}