namespace Zephyrus.Core.Exceptions;

/// <summary>
/// Thrown when a requested artifact cannot be found.
/// </summary>
public class ArtifactNotFoundException : Exception
{
    public Guid ArtifactId { get; }

    public ArtifactNotFoundException(Guid artifactId)
        : base($"Artifact with ID {artifactId} was not found.")
    {
        ArtifactId = artifactId;
    }
}
