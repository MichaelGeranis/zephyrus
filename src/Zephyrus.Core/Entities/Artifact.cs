using Zephyrus.Core.Enums;

namespace Zephyrus.Core.Entities;

/// <summary>
/// An output produced by an agent (PRD, ADR, PR, tests).
/// Content lives in GitHub — only the path is stored here.
/// </summary>
public class Artifact
{
    public Guid Id { get; private set; }
    public Guid FeatureId { get; private set; }
    public ArtifactType Type { get; private set; }

    /// <summary>
    /// Path to the artifact in the GitHub repository.
    /// </summary>
    public string GitHubPath { get; private set; } = string.Empty;

    public string? ApprovedBy { get; private set; }
    public DateTime? ApprovedAt { get; private set; }

    public Feature Feature { get; private set; } = null!;

    private Artifact() { }

    public static Artifact Create(Guid featureId, ArtifactType type, string gitHubPath)
    {
        return new Artifact
        {
            Id = Guid.NewGuid(),
            FeatureId = featureId,
            Type = type,
            GitHubPath = gitHubPath
        };
    }

    /// <summary>
    /// Marks this artifact as approved by the specified user.
    /// </summary>
    public void Approve(string approvedBy)
    {
        ApprovedBy = approvedBy;
        ApprovedAt = DateTime.UtcNow;
    }
}
