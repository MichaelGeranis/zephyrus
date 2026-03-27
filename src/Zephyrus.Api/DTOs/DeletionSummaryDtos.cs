using System.ComponentModel.DataAnnotations;

namespace Zephyrus.Api.DTOs;

/// <summary>
/// DTO representing deletion summary information for a project,
/// including the count of features and artifacts that will be deleted.
/// </summary>
public record ProjectDeletionSummaryDto
{
    /// <summary>
    /// The name of the project to be deleted.
    /// </summary>
    [Required]
    public required string ProjectName { get; init; }

    /// <summary>
    /// The number of features that will be deleted along with the project.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int FeatureCount { get; init; }

    /// <summary>
    /// The total number of artifacts across all features that will be deleted.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int ArtifactCount { get; init; }
}

/// <summary>
/// DTO representing deletion summary information for a feature,
/// including the count of artifacts that will be deleted.
/// </summary>
public record FeatureDeletionSummaryDto
{
    /// <summary>
    /// The prompt/name of the feature to be deleted.
    /// </summary>
    [Required]
    public required string FeatureName { get; init; }

    /// <summary>
    /// The number of artifacts that will be deleted along with the feature.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int ArtifactCount { get; init; }
}