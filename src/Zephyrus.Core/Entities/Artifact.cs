using Zephyrus.Core.Enums;

namespace Zephyrus.Core.Entities;

/// <summary>
/// An output produced by an agent (PRD, ADR, PR, tests).
/// Content lives in the code host — only the path is stored here.
/// </summary>
public class Artifact
{
    public Guid Id { get; private set; }
    public Guid FeatureId { get; private set; }
    public ArtifactType Type { get; private set; }

    /// <summary>
    /// Path to the artifact in the source code repository.
    /// </summary>
    public string RepositoryPath { get; private set; } = string.Empty;

    public string? ApprovedBy { get; private set; }
    public DateTime? ApprovedAt { get; private set; }

    public Feature Feature { get; private set; } = null!;

    private Artifact() { }

    public static Artifact Create(Guid featureId, ArtifactType type)
    {
        var id = Guid.NewGuid();
        return new Artifact
        {
            Id = id,
            FeatureId = featureId,
            Type = type,
            RepositoryPath = BuildRepositoryPath(type, id)
        };
    }

    public static string BuildRepositoryPath(ArtifactType type, Guid artifactId)
    {
        var folder = type switch
        {
            ArtifactType.Prd => "docs/prd",
            ArtifactType.Adr => "docs/adr",
            ArtifactType.Task => "docs/tasks",
            ArtifactType.Test => "docs/qa",
            ArtifactType.Pr => "pulls",
            ArtifactType.Workflow => ".github/workflows",
            _ => "docs"
        };

        var extension = type == ArtifactType.Workflow ? ".yml" : ".md";

        return $"{folder}/{artifactId}{extension}";
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
